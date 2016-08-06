using MCHost.Framework;
using MCHost.Framework.Minecraft;
using MCHost.Interfaces.Minecraft;
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
using System.Text.RegularExpressions;
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
        IBindingInterface BindingInterface { get; }

        InstanceConfiguration Configuration { get; set; }

        void Start();
        bool PostCommand(string command);
        bool PostShutdown();
        bool Terminate();
    }

    public interface IInstanceManager
    {
        IEnumerable<IInstance> Instances { get; }

        void AddEventListener(IInstanceEventListener listener);
        IInstance CreateInstance(Package package);
        IInstance CreateInstance(Package package, bool start);
        bool PostCommand(string instanceId, string command);
        bool PostShutdown(string instanceId);
        bool TerminateInstance(string instanceId);
        InstanceConfiguration GetInstanceConfiguration(string instanceId);
    }

    public interface IInstanceEventListener
    {
        void OnInstanceStatus(string instanceId, InstanceStatus status);
        void OnInstanceLog(string instanceId, DateTime time, string text);
    }

    public class InstanceManager : IDisposable, IInstanceManager
    {
        class Instance : IInstance, IDisposable
        {
            private readonly InstanceManager _instanceManager;
            private readonly string _id;
            private readonly Package _package;
            private readonly ILogger _logger;
            private readonly IBindingInterface _bindingInterface;

            private Thread _thread;
            private readonly ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
            private readonly ManualResetEvent _terminateEvent = new ManualResetEvent(false);

            public string Id { get { return _id; } }

            private InstanceStatus _status = InstanceStatus.Starting;
            public InstanceStatus Status { get { return _status; } }

            public Exception Exception { get; private set; }
            public Package Package { get { return _package; } }
            public IBindingInterface BindingInterface { get { return _bindingInterface; } }

            public InstanceConfiguration Configuration { get; set; }

            private readonly AutoResetEvent _executeCommandEvent = new AutoResetEvent(false);
            private LinkedList<string> _commandQueue = new LinkedList<string>();
            private readonly object _commandLock = new object();

            public Instance(InstanceManager instanceManager, string id, Package package, ILogger logger, IBindingInterface bindingInterface, ISettings settings)
            {
                _instanceManager = instanceManager;
                _id = id;
                _package = package;
                _logger = logger;
                _bindingInterface = bindingInterface;

                _status = InstanceStatus.Idle;
                Configuration = InstanceConfiguration.CreateDefault(settings);
            }

            public void Dispose()
            {
                _shutdownEvent.Set();
                _thread.Join();

                _shutdownEvent.Dispose();
                _executeCommandEvent.Dispose();

                BindingInterfaces.FreeBindingInterface(BindingInterface);
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

            private void SetStatus(InstanceStatus status)
            {
                _status = status;
                foreach (var listener in _instanceManager._instanceEventListeners)
                    listener.OnInstanceStatus(_id, _status);
            }

            private void WorkerThread()
            {
                if (!File.Exists(_package.Filename))
                {
                    _logger.Write(
                        LogType.Error,
                        $"Failed to start instance. Physical package file '{_package.Name}' ('{_package.Filename}') not found.");
                    Exception = new FileNotFoundException("The package file does not exist.", _package.Filename);
                    SetStatus(InstanceStatus.Error);
                    return;
                }

                SetStatus(InstanceStatus.Starting);

                var instanceDirectory = Path.Combine(Environment.CurrentDirectory, "instances", _id);
                Directory.CreateDirectory(instanceDirectory);

                // extract instance into a new folder and so forth
                try
                {
                    using (var stream = File.Open(_package.Filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var archive = new ZipArchive(stream))
                    {
                        archive.ExtractToDirectory(instanceDirectory);
                    }
                }
                catch (Exception ex)
                {
                    Directory.Delete(instanceDirectory);
                    _logger.Write(
                        LogType.Error,
                        $"Failed to start instance. Exception extracting package '{_package.Name}' ('{_package.Filename}') => ({ex.GetType().Name}) {ex.Message}");
                    Exception = ex;
                    SetStatus(InstanceStatus.Error);
                    return;
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
                minecraftConfig.SetValue("enable-command-block", Configuration.EnableCommandBlocks);
                minecraftConfig.SetValue("max-players", Configuration.MaxPlayers);
                minecraftConfig.SetValue("announce-player-achievements", Configuration.AnnouncePlayerAchievements);

                // network
                minecraftConfig.SetValue("server-ip", _bindingInterface.IP != "0.0.0.0" ? _bindingInterface.IP : "");
                minecraftConfig.SetValue("server-port", _bindingInterface.Port);

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
                        _logger.Write(LogType.Error, $"- Filename: {psi.FileName}");
                        _logger.Write(LogType.Error, $"- Arguments: {psi.Arguments}");
                        _logger.Write(LogType.Error, $"- Directory: {psi.WorkingDirectory}");
                        Exception = ex;
                        SetStatus(InstanceStatus.Error);
                        return;
                    }

                    _logger.Write(LogType.Notice, $"Started instance {_id}");

                    var timeMatch = new Regex(@"^\s*\[\d+:\d+(:\d+)?\]\s*");
                    var prefixMatch = new Regex(@"^\s*\[.*?\]:\s*"); // [Server thread/INFO]: etc..

                    using (process)
                    {
                        DateTime? stopTime = null;

                        process.OutputDataReceived += (sender, e) =>
                          {
                              if (e.Data != null)
                              {
                                  string msg;

                                  var match = timeMatch.Match(e.Data);
                                  if (match.Success)
                                      msg = e.Data.Substring(match.Index + match.Length);
                                  else
                                      msg = e.Data;

                                  if ((match = prefixMatch.Match(msg)).Success)
                                      msg = msg.Substring(match.Index + match.Length);

                                  InstanceLogManager.AddLog(_id, msg);

                                  _logger.Write(LogType.Normal, "[MC] " + msg);
                                  foreach (var listener in _instanceManager._instanceEventListeners)
                                      listener.OnInstanceLog(_id, DateTime.UtcNow, msg);

                                  if (msg.StartsWith("Done ("))
                                      SetStatus(InstanceStatus.Running);
                              }
                          };

                        process.BeginOutputReadLine();

                        // set process?
                        bool sentShutdown = false;

                        var waitEvents = new WaitHandle[] { _executeCommandEvent, _shutdownEvent, _terminateEvent };

                        while (!process.HasExited)
                        {
                            if (!sentShutdown &&
                                stopTime.HasValue &&
                                DateTime.UtcNow >= stopTime)
                            {
                                sentShutdown = true;
                                _logger.Write(LogType.Warning, "Sending stop command ...");
                                process.StandardInput.WriteLine("stop");

                                SetStatus(InstanceStatus.Stopping);
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
                                if (!stopTime.HasValue)
                                    stopTime = DateTime.UtcNow.AddSeconds(1);
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
                    SetStatus(InstanceStatus.Stopped);
                }
                catch (Exception ex)
                {
                    _logger.Write(LogType.Error, $"Instance loop exception detected: ({ex.GetType().Name}) {ex.Message}");
                    Exception = ex;
                    SetStatus(InstanceStatus.Error);
                }

                InstanceLogManager.Remove(_id);

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
        private readonly ISettings _settings;
        private readonly int _maxConcurrentInstances;

        public int MaxConcurrentInstances { get { return _maxConcurrentInstances; } }

        private readonly Dictionary<string, IInstance> _instances = new Dictionary<string, IInstance>();
        public IEnumerable<IInstance> Instances { get { return _instances.Values; } }

        private readonly List<IInstanceEventListener> _instanceEventListeners = new List<IInstanceEventListener>();

        public InstanceManager(ILogger logger, ISettings settings, int maxConcurrentInstances)
        {
            _logger = logger;
            _settings = settings;
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

        public void AddEventListener(IInstanceEventListener listener)
        {
            _instanceEventListeners.Add(listener);
        }

        public IInstance CreateInstance(Package package)
        {
            if (_instances.Count >= _maxConcurrentInstances)
                throw new ConcurrentInstancesExceededException();

            // AllocateBindingInterface() throws InvalidOperationException if theres no interfaces but this should never happen
            // provided the configuration prevents max concurrent instances to exceed available binding interfaces
            var bindingInterface = BindingInterfaces.AllocateBindingInterface();

            var id = DateTime.UtcNow.Ticks.ToString("x16");

            var instance = new Instance(this, id, package, _logger, bindingInterface, _settings);
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
                if (instance.Status == InstanceStatus.Stopped ||
                    instance.Status == InstanceStatus.Error)
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
