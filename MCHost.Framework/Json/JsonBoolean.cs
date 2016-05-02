using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public class JsonBoolean : JsonBase
    {
        private bool _value = false;
        public bool Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public JsonBoolean() :
            base(JsonType.Boolean)
        {
        }

        public JsonBoolean(bool value) :
            base(JsonType.Boolean)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value ? "true" : "false";
        }
    }
}
