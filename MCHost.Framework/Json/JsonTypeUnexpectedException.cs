using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public class JsonTypeUnexpectedException : JsonException
    {
        public Type JsonType { get; private set; }

        public JsonTypeUnexpectedException(Type jsonType) :
            base($"The type '{jsonType.FullName}' was unexpected.")
        {
            JsonType = jsonType;
        }
    }
}
