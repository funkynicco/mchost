using MCHost.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace MCHost.Web
{
    public class Logger : ILogger
    {
        private readonly ISettings _settings;
        private readonly string _filename;
        private readonly object _lock = new object();

        public Logger(ISettings settings)
        {
            _settings = settings;

            var directory =
                Path.IsPathRooted(settings.LogDirectory) ?
                settings.LogDirectory :
                Path.Combine(Environment.CurrentDirectory, settings.LogDirectory);

            Directory.CreateDirectory(directory); // make sure it exists

            var now = DateTime.UtcNow;

            _filename = Path.Combine(
                directory,
                $"{now.Year:0000}-{now.Month:00}-{now.Date:00} {now.Hour:00}.{now.Minute:00}.{now.Second:00}.txt");
        }

        public void Write(LogType type, string message)
        {
            lock (_lock)
            {
                File.AppendAllText(
                    _filename,
                    $"[{DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")}][{type}] {message}\r\n");
            }
        }

        public void Write(LogType type, string format, params object[] args)
        {
            Write(type, string.Format(format, args));
        }
    }
}