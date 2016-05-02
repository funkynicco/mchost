using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public class JsonString : JsonBase
    {
        private string _value = "";
        public string Value
        {
            get { return _value; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();

                _value = value;
            }
        }

        public JsonString() :
            base(JsonType.String)
        {
        }

        public JsonString(string value) :
            base(JsonType.String)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
