using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public class JsonObject : JsonBase
    {
        private readonly Dictionary<string, JsonBase> _members = new Dictionary<string, JsonBase>();
        public IDictionary<string, JsonBase> Members { get { return _members; } }

        public JsonObject() :
            base(JsonType.Object)
        {
        }

        public JsonBase GetMember(string name)
        {
            JsonBase value;
            if (!_members.TryGetValue(name, out value))
                throw new JsonMemberNotFoundException(name);

            return value;
        }

        public T GetMember<T>(string name) where T : JsonBase
        {
            var member = GetMember(name);
            if (member.GetType() != typeof(T))
                throw new JsonTypeUnexpectedException(typeof(T));

            return (T)member;
        }

        public void Read(ref string json, ref int i) // expects "{...}"
        {
            if (json[i] != '{')
                throw new JsonParseException("Invalid JsonObject at offset: " + i);

            ++i;
            while (i < json.Length)
            {
                SkipWhitespace(ref json, ref i, true);
                if (json[i] == '}')
                {
                    ++i;
                    return;
                }
                else if (json[i] == ',')
                {
                    ++i;
                    continue;
                }

                var name = ReadString(ref json, ref i);
                SkipWhitespace(ref json, ref i, true);
                if (json[i++] != ':')
                    throw new JsonParseException("Invalid json at offset: " + i);
                SkipWhitespace(ref json, ref i, true);

                _members.Add(name, ReadValue(ref json, ref i));
            }
        }
    }
}
