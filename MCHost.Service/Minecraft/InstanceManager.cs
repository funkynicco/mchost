using MCHost.Framework;
using MCHost.Framework.Minecraft;
using MCHost.Service.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MCHost.Service.Minecraft
{
    public interface IInstance
    {
        string Id { get; }
        InstanceStatus Status { get; }
        Exception Exception { get; }
        Package Package { get; }

        InstanceConfiguration Configuration { get; set; }

        void Start();
        bool PostCommand(string command);
        bool PostShutdown();
        bool Terminate();
    }

    public interface IInstanceManager
    {
        IEnumerable<IInstance> Instances { get; }
        IInstance CreateInstance(Package package);
        IInstance CreateInstance(Package package, bool start);
        bool PostCommand(string instanceId, string command);
        bool PostShutdown(string instanceId);
        bool TerminateInstance(string instanceId);
        InstanceConfiguration GetInstanceConfiguration(string instanceId);
    }

    public class InstanceManager : IDisposable, IInstanceManager
    {
        class Instance : IInstance, IDisposable
        {
            private readonly string _id;
            private readonly Package _package;
            private readonly ILogger _logger;
            private readonly IServer _server;

            private Thread _thread;
            private readonly ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
            private readonly ManualResetEvent _terminateEvent = new ManualResetEvent(false);

            public string Id { get { return _id; } }

            private InstanceStatus _status = InstanceStatus.Starting;
            public InstanceStatus Status { get { return _status; } }

            public Exception Exception { get; private set; }
            public Package Package { get { return _package; } }

            public InstanceConfiguration Configuration { get; set; }

            private readonly AutoResetEvent _executeCommandEvent = new AutoResetEvent(false);
            private LinkedList<string> _commandQueue = new LinkedList<string>();
            private readonly object _commandLock = new object();

            public Instance(string id, Package package, ILogger logger, IServer server)
            {
                _id = id;
                _package = package;
                _logger = logger;
                _server = server;

                _status = InstanceStatus.Idle;
                Configuration = InstanceConfiguration.Default;
            }

            public void Dispose()
            {
                _shutdownEvent.Set();
                _thread.Join();

                _shutdownEvent.Dispose();
                _executeCommandEvent.Dispose();
            }

            public void Start()
            {
                if (_thread != null)
                    throw new InvalidOperationException("Instance already started.");

                Configuration.Validate();

                _thread = new Thread(WorkerThread);
                _thread.Start();
            }

            public bool PostCommand(string command)
            {
                if (_status != InstanceStatus.Starting &&
                    _status != InstanceStatus.Running)
                    return false;

                lock (_commandLock)
                {
                    _commandQueue.AddLast(command);
                    _executeCommandEvent.Set();
                }

                return true;
            }

            public bool PostShutdown()
            {
                if (_status != InstanceStatus.Starting &&
                    _status != InstanceStatus.Running)
                    return false;

                _shutdownEvent.Set();
                return true;
            }

            public bool Terminate()
            {
                if (_status != InstanceStatus.Starting &&
                    _status != InstanceStatus.Running)
                    return false;

                _terminateEvent.Set();
                return true;
            }

            private void WorkerThread()
            {
                _server.BroadcastInstanceStatus(_id, _status = InstanceStatus.Starting);

                var instanceDirectory = Path.Combine(Environment.CurrentDirectory, "instances", _id);
                Directory.CreateDirectory(instanceDirectory);

                // extract instance into a new folder and so forth
                using (var stream = File.Open(_package.Filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var archive = new ZipArchive(stream))
                {
                    archive.ExtractToDirectory(instanceDirectory);
                }

                // always set the eula to true regardless of the package
                File.WriteAllText(Path.Combine(instanceDirectory, "eula.txt"), "eula=true\r\n");

                // Configure the instance based on given settings
                var minecraftConfig = MinecraftConfiguration.FromFile(Path.Combine(instanceDirectory, "server.properties"));

                minecraftConfig.SetValue("snooper-enabled", "false"); // don't send snoop data to snoop.minecraft.net

                foreach (var extraConfiguration in Configuration.ExtraConfigurationValues)
                {
                    minecraftConfig.SetValue(extraConfiguration.Key, extraConfiguration.Value);
                }

                minecraftConfig.SetValue("motd", Configuration.Motd);
                minecraftConfig.SetValue("enable-command-block", Configuration.EnableCommandBlocks ? "true" : "false");
                minecraftConfig.SetValue("max-players", Configuration.MaxPlayers.ToString());
                minecraftConfig.SetValue("announce-player-achievements", Configuration.AnnouncePlayerAchievements ? "true" : "false");

                // network
                var bindAddress = Configuration.BindInterface.Split(':')[0];
                minecraftConfig.SetValue("server-ip", bindAddress != "0.0.0.0" ? bindAddress : "");
                minecraftConfig.SetValue("server-port", Configuration.BindInterface.Split(':')[1]);

                minecraftConfig.Save();

                try
                {
                    // start process
                    var psi = new ProcessStartInfo(
                        Configuration.JavaExecutable,
                        $"-Xmx{Configuration.JavaMaximumMemoryMegabytes}M -Xms{Configuration.JavaInitialMemoryMegabytes}M -jar \"{Configuration.MinecraftJarFilename}\" nogui");
                    psi.UseShellExecute = false;
                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardOutput = true;
                    psi.CreateNoWindow = true;
                    psi.WorkingDirectory = instanceDirectory;

                    Process process;
                    try
                    {
                        process = Process.Start(psi);

                        if (process == null)
                            throw new Exception("Process.Start returned null");
                    }
                    catch (Exception ex)
                    {
                        _logger.Write(LogType.Error, $"Exception in starting process: ({ex.GetType().Name}) {ex.Message}");
                        return;
                    }

                    _logger.Write(LogType.Notice, $"Started instance {_id}");

                    using (process)
                    {
                        int stopTime = 0;

                        process.OutputDataReceived += (sender, e) =>
                          {
                              if (e.Data != null)
                              {
                                  _logger.Write(LogType.Normal, "[MC] " + e.Data);
                                  _server.BroadcastInstanceLog(_id, e.Data);

                                  if (e.Data.Contains(": Done"))
                                      _server.BroadcastInstanceStatus(_id, _status = InstanceStatus.Running);
                              }
                          };

                        process.BeginOutputReadLine();

                        // set process?
                        bool sentShutdown = false;

                        var waitEvents = new WaitHandle[] { _executeCommandEvent, _shutdownEvent, _terminateEvent };

                        while (!process.HasExited)
                        {
                            if (!sentShutdown &&
                                stopTime > 0 &&
                                Environment.TickCount >= stopTime)
                            {
                                sentShutdown = true;
                                _logger.Write(LogType.Warning, "Sending stop command ...");
                                process.StandardInput.WriteLine("stop");
                                _server.BroadcastInstanceStatus(_id, _status = InstanceStatus.Stopping);
                            }

                            int n = WaitHandle.WaitAny(waitEvents, 100); // WaitHandle.WaitTimeout on timeout ...
                            if (n == 0)//_executeCommandEvent.WaitOne(100))
                            {
                                LinkedList<string> commands;
                                lock (_commandLock)
                                {
                                    commands = _commandQueue;
                                    _commandQueue = new LinkedList<string>();
                                }

                                foreach (var command in commands)
                                {
                                    process.StandardInput.WriteLine(command);
                                }
                            }
                            else if (n == 1) // gracefully shutdown
                            {
                                if (stopTime <= 0)
                                    stopTime = Environment.TickCount + 1;
                            }
                            else if (n == 2) // terminate process
                            {
                                // terminate
                                _logger.Write(LogType.Warning, $"Instance {_id} forcefully killed.");
                                process.Kill();
                            }
                        }

                        process.CancelOutputRead();
                    }

                    _logger.Write(LogType.Notice, $"Instance {_id} stopped");
                    _server.BroadcastInstanceStatus(_id, _status = InstanceStatus.Stopped);
                }
                catch (Exception ex)
                {
                    Exception = ex;
                    _status = InstanceStatus.Error;
                    throw;
                }

                // delete instance
                while (true)
                {
                    try
                    {
                        Directory.Delete(instanceDirectory, true);
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(500);
                    }
                }
            }
        }

        private readonly ILogger _logger;
        private readonly IServer _server;
        private readonly int _maxConcurrentInstances;

        public int MaxConcurrentInstances { get { return _maxConcurrentInstances; } }

        private readonly Dictionary<string, IInstance> _instances = new Dictionary<string, IInstance>();
        public IEnumerable<IInstance> Instances { get { return _instances.Values; } }

        public InstanceManager(ILogger logger, IServer server, int maxConcurrentInstances)
        {
            _logger = logger;
            _server = server;
            _maxConcurrentInstances = maxConcurrentInstances;
        }

        public void Dispose()
        {
            foreach (var instance in _instances.Values)
            {
                (instance as Instance).Dispose();
            }
            _instances.Clear();
        }

        public IInstance CreateInstance(Package package)
        {
            if (_instances.Count >= _maxConcurrentInstances)
                throw new ConcurrentInstancesExceededException();

            var id = DateTime.UtcNow.Ticks.ToString("x16");

            var instance = new Instance(id, package, _logger, _server);
            _instances.Add(id, instance);
            return instance;
        }

        public IInstance CreateInstance(Package package, bool start)
        {
            var instance = CreateInstance(package);
            if (start)
                instance.Start();
            return instance;
        }

        public bool PostCommand(string instanceId, string command)
        {
            IInstance instance;
            if (!_instances.TryGetValue(instanceId, out instance))
                return false;

            return (instance as Instance).PostCommand(command);
        }

        public bool PostShutdown(string instanceId)
        {
            IInstance instance;
            if (!_instances.TryGetValue(instanceId, out instance))
                return false;

            return (instance as Instance).PostShutdown();
        }

        public bool TerminateInstance(string instanceId)
        {
            IInstance instance;
            if (!_instances.TryGetValue(instanceId, out instance))
                return false;

            return (instance as Instance).Terminate();
        }

        public void RemoveDeadInstances()
        {
            var instancesToRemove = new List<Instance>();

            foreach (var instance in _instances.Values)
            {
                if (instance.Status == InstanceStatus.Stopped)
                    instancesToRemove.Add((Instance)instance);
            }

            foreach (var instance in instancesToRemove)
            {
                _instances.Remove(instance.Id);
                _logger.Write(LogType.Notice, $"RemoveDeadInstances() - {instance.Id}");
                instance.Dispose();
            }
        }

        /// <summary>
        /// Gets the instance configuration.
        /// <para>* Do not edit any parameters as they may be accessed by another thread.</para>
        /// </summary>
        /// <param name="instanceId">Instance ID</param>
        public InstanceConfiguration GetInstanceConfiguration(string instanceId)
        {
            IInstance instance;
            if (!_instances.TryGetValue(instanceId, out instance))
                return null;

            return (instance as Instance).Configuration;
        }
    }

    public class ConcurrentInstancesExceededException : Exception
    {
        public ConcurrentInstancesExceededException() :
            base("The maximum amount of concurrent instances has been exceeded.")
        {
        }
    }
}
