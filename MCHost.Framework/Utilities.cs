using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MCHost.Framework
{
    public static class Utilities
    {
        public static string EscapeWebSocketContent(string str)
        {
            var sb = new StringBuilder(str.Length);

            for (int i = 0; i < str.Length; ++i)
            {
                var ch = str[i];

                switch (ch)
                {
                    case '%':
                        sb.Append("%25");
                        break;
                    case '|':
                        sb.Append("%7C");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }

        public static string Unescape(string str)
        {
            var sb = new StringBuilder(str.Length);

            for (int i = 0; i < str.Length;)
            {
                //%00
                if (str[i] == '%' &&
                    i + 2 < str.Length)
                {
                    var hex = str.Substring(i + 1, 2).ToUpper();

                    if (!Regex.IsMatch(hex, "^[0-9A-F]{2}$"))
                        throw new FormatException($"Not a valid hex code: {hex}");

                    sb.Append((char)byte.Parse(hex, NumberStyles.HexNumber));

                    i += 3;
                    continue;
                }

                sb.Append(str[i++]);
            }

            return sb.ToString();
        }

        public static string DecodeAsciiString(string str)
        {
            var sb = new StringBuilder(str.Length);

            for (int i = 0; i < str.Length;)
            {
                //&#58;
                if (str[i] == '&' &&
                    i + 3 < str.Length && // &#0;
                    str[i + 1] == '#')
                {
                    var x = str.IndexOf(';', i + 2);
                    if (x != -1)
                    {
                        var mid = str.Substring(i + 2, x - (i + 2));
                        int val;
                        if (!int.TryParse(mid, out val))
                            throw new FormatException($"The ASCII escaping sequence &#{mid}; is not valid.");

                        char ch = (char)val;
                        sb.Append(ch);
                        i = x + 1;
                        continue;
                    }
                }

                sb.Append(str[i++]);
            }

            return sb.ToString();
        }
    }
}
