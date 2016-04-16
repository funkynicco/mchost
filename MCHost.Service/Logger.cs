using MCHost.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Service
{
    public class Logger : ILogger
    {
        private readonly object _lock = new object();

        public void Write(LogType type, string message)
        {
            lock (_lock)
            {
                Console.Write("[{0}] ", DateTime.Now.ToString("HH:mm:ss"));

                switch (type)
                {
                    case LogType.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogType.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogType.Notice:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        break;
                    case LogType.Success:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                }

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(message);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        public void Write(LogType type, string format, params object[] args)
        {
            Write(type, string.Format(format, args));
        }
    }
}
