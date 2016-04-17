using MCHost.Framework;
using MCHost.Service;
using MCHost.Service.Minecraft;
using MCHost.Service.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MCHost
{
    static class Program
    {
        private static IDatabase _database;

        public static string MinecraftRoot { get; private set; }

        /// <summary>
        /// Looks for an environment variable first in the process, then user variables and finally system variables.
        /// </summary>
        /// <param name="name">Name of environment variable</param>
        static string GetEnvironmentVariable(string name)
        {
            var path = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process); // batch bootstrapping
            if (path != null)
                return path;

            path = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User); // user variables
            if (path != null)
                return path;

            return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine); // system variables
        }

        enum Result : int
        {
            Exception = -1,
            Succeeded,
            DatabaseConnectionFailed,
            HostVariableNotFound,
            ServiceBindingInvalid,
            FailedToBindInterface
        }

        static Result SubMain(ILogger logger)
        {
            var configuration = Configuration.Load("configuration.xml");
            _database = Database.Create(logger, configuration["ConnectionString"], ServiceType.InstanceService);

            logger.Write(LogType.Notice, "Testing database connection ...");

            try
            {
                _database.TestConnection();
            }
            catch (Exception ex)
            {
                _database = null;
                logger.Write(LogType.Error, "Failed to connect to database.");
                logger.Write(LogType.Error, $"({ex.GetType().Name}): {ex.Message}");
                return Result.DatabaseConnectionFailed;
            }

            logger.Write(LogType.Normal, "DB OK");

            var minecraftRoot = GetEnvironmentVariable("MCHOST_MINECRAFT");
            if (minecraftRoot == null ||
                !Directory.Exists(minecraftRoot = Path.GetFullPath(minecraftRoot)))
            {
                if (minecraftRoot == null)
                    logger.Write(LogType.Error, "The MCHOST_MINECRAFT environment variable was not provided.");
                else
                    logger.Write(LogType.Error, "The MCHOST_MINECRAFT path is not pointing to a valid directory.");
                return Result.HostVariableNotFound;
            }

            MinecraftRoot = minecraftRoot;

            var serviceBindingMatch = Regex.Match(configuration["ServiceBinding"], @"^([0-9.]+):(\d+)$", RegexOptions.IgnoreCase);
            if (!serviceBindingMatch.Success)
            {
                logger.Write(LogType.Error, "The ServiceBinding is invalid in the configuration file. The format is ip:port");
                logger.Write(LogType.Error, "The IP 0.0.0.0 can be used to bind on all network interfaces.");
                return Result.ServiceBindingInvalid;
            }

            var ip = serviceBindingMatch.Groups[1].Value;
            var port = int.Parse(serviceBindingMatch.Groups[2].Value);

            using (var server = new Server(logger, _database))
            using (var instanceManager = new InstanceManager(logger, server))
            {
                server.SetInstanceManager(instanceManager);

                try
                {
                    server.Start(new IPEndPoint[] { new IPEndPoint(IPAddress.Parse(ip), port) });
                }
                catch (Exception ex)
                {
                    logger.Write(LogType.Error, $"Failed to bind on interface {ip}:{port}");
                    logger.Write(LogType.Error, $"({ex.GetType().Name}): {ex.Message}");
                    return Result.FailedToBindInterface;
                }

                _database.AddLog("Started minecraft service host.", true);

                var nextInstanceProcess = DateTime.UtcNow.AddSeconds(1);

                while (true)
                {
                    var now = DateTime.UtcNow;

                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);

                        if (key.Key == ConsoleKey.F1)
                        {
                            var package = new Package()
                            {
                                Name = "MCInstance",
                                Description = "",
                                Filename = @"packages\test.zip"
                            };
                            instanceManager.CreateInstance(package, true);
                        }

                        if (key.Key == ConsoleKey.Escape)
                            break;
                    }

                    server.Process();

                    if (now >= nextInstanceProcess)
                    {
                        instanceManager.RemoveDeadInstances();
                        nextInstanceProcess = now.AddSeconds(1);
                    }

                    Thread.Sleep(50);
                }

                _database.AddLog("Stopped minecraft service host.", true);
            }

            return 0;
        }

        static int Main()
        {
            Console.Title = "MCHost Service";

            var logger = new Logger();

            int result = 0;

#if !DEBUG
            try
            {
#endif // !DEBUG
            result = (int)SubMain(logger);
#if !DEBUG
            }
            catch (Exception ex)
            {
                if (_database != null)
                    _database.AddLog("MCHost service crashed with exception " + ex.GetType().Name + ": " + ex.Message);
                logger.Write(LogType.Error, "MCHost service crashed with exception " + ex.GetType().Name + ": " + ex.Message);
                result = -1;
            }
#endif // DEBUG

#if DEBUG
            if (result != 0)
            {
                logger.Write(LogType.Normal, "### (Press any key to continue) Exit code: " + result);
                Console.ReadKey(true);
            }
#endif // DEBUG

            return result;
        }
    }
}