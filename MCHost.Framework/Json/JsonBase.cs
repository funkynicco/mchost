using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Json
{
    public abstract class JsonBase
    {
        private readonly JsonType _type;
        public JsonType Type { get { return _type; } }

        protected JsonBase(JsonType type)
        {
            _type = type;
        }

        public static void SkipWhitespace(ref string json, ref int i, bool throwIfEof)
        {
            while (i < json.Length)
            {
                var skip = false;

                switch (json[i])
                {
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        skip = true;
                        break;
                }

                if (!skip)
                    break;

                ++i;
            }

            if (i >= json.Length &&
                throwIfEof)
                throw new JsonParseException("EOF");
        }

        public static JsonBase ReadValue(ref string json, ref int i)
        {
            char ch = json[i];

            if (ch == '"')
                return new JsonString(ReadString(ref json, ref i));

            if (char.IsDigit(ch) ||
                (ch == '-' &&
                i + 1 < json.Length &&
                char.IsDigit(json[i + 1])))
                return new JsonNumber(ReadNumber(ref json, ref i));

            if (ch == '{')
            {
                var obj = new JsonObject();
                obj.Read(ref json, ref i);
                return obj;
            }

            if (ch == '[')
            {
                var ary = new JsonArray();
                ary.Read(ref json, ref i);
                return ary;
            }

            if (i + 4 <= json.Length &&
                json.Substring(i, 4).ToLower() == "true")
            {
                i += 4;
                return new JsonBoolean(true);
            }

            if (i + 5 <= json.Length &&
                json.Substring(i, 5).ToLower() == "false")
            {
                i += 5;
                return new JsonBoolean(false);
            }

            if (i + 4 <= json.Length &&
                json.Substring(i, 4).ToLower() == "null")
            {
                i += 4;
                return null;
            }

            throw new JsonParseException("ReadValue - No suitable json value found at index: " + i);
        }

        public static long ReadNumber(ref string json, ref int i)
        {
            var value = 0L;
            var first = true;
            var isNegative = false;

            while (i < json.Length)
            {
                if (first &&
                    json[i] == '-' &&
                    !isNegative)
                {
                    isNegative = true;
                    ++i;
                    continue;
                }

                if (!char.IsDigit(json[i]))
                    return value;

                if (!first)
                    value *= 10;

                value += (byte)json[i++] - (byte)'0';

                first = false;
            }

            if (isNegative)
                return -value;

            return value;
        }

        public static string ReadString(ref string json, ref int i) // expects "\"...\""
        {
            if (json[i] != '"')
                throw new JsonParseException("Invalid Json string at offset: " + i);

            var sb = new StringBuilder();

            ++i;
            var start = i;
            while (i < json.Length)
            {
                if (json[i] == '\\')
                {
                    if (i + 1 >= json.Length)
                        throw new JsonParseException("EOF");

                    var ch = json[++i];
                    switch (ch)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            sb.Append(ch);
                            break;
                        case 'b':
                            sb.Append('\b');
                            break;
                        case 'f':
                            sb.Append('\f');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'u':
                            throw new JsonParseException("\\u0000 is not supported.");
                    }

                    ++i;
                }
                else if (json[i] == '"')
                {
                    ++i;
                    return sb.ToString();
                }

                sb.Append(json[i++]);
            }

            if (i >= json.Length)
                throw new JsonParseException("EOF");

            return sb.ToString();
        }
    }
}
