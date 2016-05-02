using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public class JsonException : Exception
    {
        public JsonException()
        {
        }

        public JsonException(string message) :
            base(message)
        {
        }
    }
}
