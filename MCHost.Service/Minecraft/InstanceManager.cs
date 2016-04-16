using MCHost.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MCHost.Service.Minecraft
{
    public enum InstanceStatus
    {
        Starting,
        Started,
        Stopping,
        Stopped,
        Error
    }

    public interface IInstance
    {
        string Id { get; }
        InstanceStatus Status { get; }
        Exception Exception { get; }
    }

    public class InstanceManager : IDisposable
    {
        class Instance : IInstance, IDisposable
        {
            private readonly string _id;
            private readonly Package _package;
            private readonly ILogger _logger;

            private readonly Thread _thread;
            private readonly ManualResetEvent _closeEvent = new ManualResetEvent(false);

            public string Id { get { return _id; } }

            private InstanceStatus _status = InstanceStatus.Starting;
            public InstanceStatus Status { get { return _status; } }

            public Exception Exception { get; private set; }

            public Instance(string id, Package package, ILogger logger)
            {
                _id = id;
                _package = package;
                _logger = logger;

                _thread = new Thread(WorkerThread);
                _thread.Start();
            }

            public void Dispose()
            {
                _closeEvent.Set();
                _thread.Join();

                _closeEvent.Dispose();
            }

            private void WorkerThread()
            {
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
                minecraftConfig.SetValue("motd", _package.Name);
                minecraftConfig.SetValue("enable-command-block", "true");
                minecraftConfig.SetValue("max-players", "20");
                minecraftConfig.SetValue("server-port", "25565");
                minecraftConfig.SetValue("announce-player-achievements", "false"); // configurable by website?
                minecraftConfig.Save();

                try
                {
                    // start process
                    var psi = new ProcessStartInfo("java", "-Xmx1024M -Xms1024M -jar server.jar nogui");
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
                                  if (e.Data.Contains("Done"))
                                      stopTime = Environment.TickCount + 1000;
                              }
                          };

                        process.BeginOutputReadLine();

                        // set process?
                        bool sentShutdown = false;

                        while (!process.HasExited)
                        {
                            if (!sentShutdown &&
                                stopTime > 0 &&
                                Environment.TickCount >= stopTime)
                            {
                                sentShutdown = true;
                                _logger.Write(LogType.Warning, "Sending stop command ...");
                                process.StandardInput.WriteLine("stop");
                            }

                            // dudledafflie
                            Thread.Sleep(100);
                        }

                        process.CancelOutputRead();
                    }

                    _logger.Write(LogType.Notice, $"Instance {_id} stopped");
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

        private readonly Dictionary<string, IInstance> _instances = new Dictionary<string, IInstance>();
        public IEnumerable<IInstance> Instances { get { return _instances.Values; } }

        public InstanceManager(ILogger logger)
        {
            _logger = logger;
        }

        public void Dispose()
        {
            foreach (var instance in _instances.Values)
            {
                (instance as Instance).Dispose();
            }
            _instances.Clear();
        }

        public IInstance StartInstance(Package package)
        {
            var id = DateTime.UtcNow.Ticks.ToString("x16");

            var instance = new Instance(id, package, _logger);
            _instances.Add(id, instance);
            return instance;
        }
    }
}
