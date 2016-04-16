using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework
{
    public interface ILogger
    {
        void Write(LogType type, string message);
        void Write(LogType type, string format, params object[] args);
    }

    public enum LogType
    {
        Normal,
        Error,
        Warning,
        Notice,
        Success
    }
}
