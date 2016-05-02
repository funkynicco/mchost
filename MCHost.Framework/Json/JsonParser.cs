using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public static class JsonParser
    {
        /// <summary>
        /// Parses json data and returns the either an object or array as result.
        /// </summary>
        /// <exception cref="JsonParseException"></exception>
        public static JsonBase Parse(string json)
        {
            int i = 0;
            JsonBase.SkipWhitespace(ref json, ref i, true);

            JsonBase obj = null;
            if (json[i] == '{')
            {
                obj = new JsonObject();
                (obj as JsonObject).Read(ref json, ref i);
            }
            else if (json[i] == '[')
            {
                obj = new JsonArray();
                (obj as JsonArray).Read(ref json, ref i);
            }

            if (obj == null)
                throw new JsonParseException("Invalid JSON was provided.");

            return obj;
        }

        /// <summary>
        /// Parses json data and returns the requested type. If the result type is not the requested type, JsonParseException will be thrown.
        /// </summary>
        /// <exception cref="JsonParseException"></exception>
        public static T Parse<T>(string json) where T : JsonBase
        {
            var data = Parse(json);
            if (data.GetType() != typeof(T))
                throw new JsonParseException("Json type was not expected.");

            return (T)data;
        }
    }
}
