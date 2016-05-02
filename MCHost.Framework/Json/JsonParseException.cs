using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public class JsonParseException : JsonException
    {
        public JsonParseException(string message) :
            base(message)
        {
        }
    }
}
