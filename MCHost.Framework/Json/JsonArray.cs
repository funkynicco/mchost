using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public class JsonArray : JsonBase
    {
        private readonly List<JsonBase> _items = new List<JsonBase>();
        public IEnumerable<JsonBase> Items { get { return _items; } }

        public JsonArray() :
            base(JsonType.Array)
        {
        }

        public void Read(ref string json, ref int i) // expects "[...]"
        {
            if (json[i] != '[')
                throw new JsonParseException("Invalid JsonArray at offset: " + i);

            ++i;
            while (i < json.Length)
            {
                SkipWhitespace(ref json, ref i, true);
                if (json[i] == ']')
                {
                    ++i;
                    return;
                }
                else if (json[i] == ',')
                {
                    ++i;
                    continue;
                }

                _items.Add(ReadValue(ref json, ref i));
            }
        }
    }
}
