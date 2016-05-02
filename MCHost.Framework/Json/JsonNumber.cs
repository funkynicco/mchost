using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public class JsonNumber : JsonBase
    {
        private long _value = 0L;
        public long Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public JsonNumber() :
            base(JsonType.Number)
        {
        }

        public JsonNumber(long value) :
            base(JsonType.Number)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
