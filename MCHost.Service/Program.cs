using MCHost.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MCHost
{
    static class Program
    {
        /*using (var stream = File.Open(@"D:\Coding\Public Projects\MCHost\server\server.zip", FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var archive = new ZipArchive(stream))
        {
            //int a = 0;
        }*/

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

        static int SubMain()
        {
            var configuration = Configuration.Load("configuration.xml");
            var database = Database.Create(configuration["ConnectionString"]);

            var minecraftRoot = GetEnvironmentVariable("MCHOST_MINECRAFT");
            if (minecraftRoot != null)
                Console.WriteLine("root: " + minecraftRoot);

            if (minecraftRoot == null ||
                !Directory.Exists(minecraftRoot = Path.GetFullPath(minecraftRoot)))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                if (minecraftRoot == null)
                    Console.WriteLine("The MCHOST_MINECRAFT environment variable was not provided.");
                else
                    Console.WriteLine("The MCHOST_MINECRAFT path is not pointing to a valid directory.");
                Console.ForegroundColor = ConsoleColor.Gray;
                return 1;
            }

            foreach (var package in database.GetPackages())
            {
                Console.WriteLine($"{package.Name} - {package.Description} - {package.Filename}");
            }

            return 0;
        }

        static int Main()
        {
            Console.Title = "MCHost Service";

            var result = SubMain();

#if DEBUG
            Console.WriteLine("### (Press any key to continue) Exit code: " + result);
            Console.ReadKey(true);
#endif // DEBUG

            return result;
        }
    }
}