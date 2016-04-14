using MCHost.Framework;
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

        static Result SubMain()
        {
            /*using (var stream = File.Open(@"D:\Coding\Public Projects\MCHost\server\server.zip", FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(stream))
            {
                //int a = 0;
            }*/

            var configuration = Configuration.Load("configuration.xml");
            _database = Database.Create(configuration["ConnectionString"], ServiceType.InstanceService);

            Console.WriteLine("Testing database connection ...");

            try
            {
                _database.TestConnection();
            }
            catch (Exception ex)
            {
                _database = null;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to connect to database.");
                Console.WriteLine($"({ex.GetType().Name}): {ex.Message}");
                Console.ForegroundColor = ConsoleColor.Gray;
                return Result.DatabaseConnectionFailed;
            }

            Console.WriteLine("DB OK");

            var minecraftRoot = GetEnvironmentVariable("MCHOST_MINECRAFT");
            if (minecraftRoot == null ||
                !Directory.Exists(minecraftRoot = Path.GetFullPath(minecraftRoot)))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                if (minecraftRoot == null)
                    Console.WriteLine("The MCHOST_MINECRAFT environment variable was not provided.");
                else
                    Console.WriteLine("The MCHOST_MINECRAFT path is not pointing to a valid directory.");
                Console.ForegroundColor = ConsoleColor.Gray;
                return Result.HostVariableNotFound;
            }

            MinecraftRoot = minecraftRoot;

            var serviceBindingMatch = Regex.Match(configuration["ServiceBinding"], @"^([0-9.]+):(\d+)$", RegexOptions.IgnoreCase);
            if (!serviceBindingMatch.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The ServiceBinding is invalid in the configuration file. The format is ip:port");
                Console.WriteLine("The IP 0.0.0.0 can be used to bind on all network interfaces.");
                Console.ForegroundColor = ConsoleColor.Gray;
                return Result.ServiceBindingInvalid;
            }

            var ip = serviceBindingMatch.Groups[1].Value;
            var port = int.Parse(serviceBindingMatch.Groups[2].Value);

            using (var server = new Server())
            {
                try
                {
                    server.Start(new IPEndPoint[] { new IPEndPoint(IPAddress.Parse(ip), port) });
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to bind on interface {ip}:{port}");
                    Console.WriteLine($"({ex.GetType().Name}): {ex.Message}");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    return Result.FailedToBindInterface;
                }

                _database.AddLog("Started minecraft service host.", true);

                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Escape)
                            break;
                    }

                    server.Process();
                    Thread.Sleep(50);
                }

                _database.AddLog("Stopped minecraft service host.", true);
            }

            return 0;
        }

        static int Main()
        {
            Console.Title = "MCHost Service";

            int result = 0;

#if !DEBUG
            try
            {
#endif // !DEBUG
            result = (int)SubMain();
#if !DEBUG
            }
            catch (Exception ex)
            {
                if (_database != null)
                    _database.AddLog("MCHost service crashed with exception " + ex.GetType().Name + ": " + ex.Message);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("MCHost service crashed with exception " + ex.GetType().Name + ": " + ex.Message);
                Console.ForegroundColor = ConsoleColor.Gray;
                result = -1;
            }
#endif // DEBUG

#if DEBUG
            if (result != 0)
            {
                Console.WriteLine("### (Press any key to continue) Exit code: " + result);
                Console.ReadKey(true);
            }
#endif // DEBUG

            return result;
        }
    }
}