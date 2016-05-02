using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.WebSockets
{
    public enum HttpRequestMethod
    {
        /// <summary>
        /// The request method is unknown.
        /// </summary>
        Unknown,
        /// <summary>
        /// Retrieve a resource.
        /// </summary>
        Get,
        /// <summary>
        /// Post data.
        /// </summary>
        Post,
        /// <summary>
        /// Update data.
        /// </summary>
        Put,
        /// <summary>
        /// Delete a resource.
        /// </summary>
        Delete,
        /// <summary>
        /// Request a list of allowed HTTP request methods for this resource.
        /// </summary>
        Options,
        /// <summary>
        /// Same header response as HTTP GET would generate but content must not be sent.
        /// </summary>
        Head
    }

    public class HttpLocation : IEnumerable<KeyValuePair<string, string>>
    {
        public string Url { get; set; }

        private readonly Dictionary<string, string> _parameters = new Dictionary<string, string>();

        public int ParameterCount { get { return _parameters.Count; } }

        public HttpLocation(string location)
        {
            int pos;
            if ((pos = location.IndexOf('?')) != -1)
            {
                Url = location.Substring(0, pos);
                location = location.Substring(pos + 1);
                if (location.Length > 0)
                {
                    foreach (var kv in location.Split('&'))
                    {
                        if ((pos = kv.IndexOf('=')) != -1)
                        {
                            var decodedParameterValue = DecodeUrl(kv.Substring(pos + 1));

                            _parameters.Add(
                                kv.Substring(0, pos).ToLower(),
                                decodedParameterValue);
                        }
                        else
                            _parameters.Add(kv.ToLower(), "");
                    }
                }
            }
            else
                Url = location;
        }

        public string GetParameter(string name)
        {
            string value = null;
            _parameters.TryGetValue(name.ToLower(), out value);
            return value;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }

        public static string EncodeUrl(string url)
        {
            var sb = new StringBuilder(url.Length + 16);

            foreach (var c in url)
            {
                sb.Append("%" + ((byte)c).ToString("X2"));
            }

            return sb.ToString();
        }

        public static string DecodeUrl(string url)
        {
            var sb = new StringBuilder(url.Length + 16);

            for (int i = 0; i < url.Length;)
            {
                if (url[i] == '%' &&
                    i + 2 < url.Length)
                {
                    var ch = byte.Parse(url.Substring(i + 1, 2), NumberStyles.HexNumber);
                    if (ch == 0)
                        throw new UrlDecodingException(url);

                    sb.Append((char)ch);
                    i += 2;
                    continue;
                }

                sb.Append(url[i++]);
            }

            return sb.ToString();
        }
    }

    public class UrlDecodingException : Exception
    {
        public UrlDecodingException(string url) :
            base(string.Format("Invalid URL for decoding: " + url))
        {
        }
    }

    public class HttpHeader : IEnumerable<KeyValuePair<string, string>>
    {
        private readonly HttpRequestMethod _method;
        private readonly HttpLocation _location;
        private readonly string _version;
        private readonly IDictionary<string, string> _parameters;
        private readonly Dictionary<string, string> _cookies = new Dictionary<string, string>();

        /// <summary>
        /// Gets the method used in the request.
        /// </summary>
        public HttpRequestMethod Method { get { return _method; } }
        /// <summary>
        /// Gets the virtual URL location of the request.
        /// </summary>
        public HttpLocation Location { get { return _location; } }
        /// <summary>
        /// Gets the HTTP version.
        /// </summary>
        public string Version { get { return _version; } }
        /// <summary>
        /// Gets the amount of header parameters.
        /// </summary>
        public int ParameterCount { get { return _parameters.Count; } }

        public IEnumerable<string> Cookies { get { return _cookies.Values; } }

        private HttpHeader(
            HttpRequestMethod method,
            HttpLocation location,
            string version,
            IDictionary<string, string> parameters)
        {
            _method = method;
            _location = location;
            _version = version;
            _parameters = parameters;

            // parse cookies
            string cookieString;
            if (_parameters.TryGetValue("cookie", out cookieString))
                ParseCookieString(cookieString, _cookies);
        }

        public bool GetParameter(string name, ref string result)
        {
            name = name.ToLower();

            string value;
            if (!_parameters.TryGetValue(name, out value))
                return false;

            result = value;
            return true;
        }

        public bool GetParameter(string name, ref int result)
        {
            string param = string.Empty;
            if (!GetParameter(name, ref param))
                return false;

            int n;
            if (!int.TryParse(param, out n))
                return false;

            result = n;
            return true;
        }

        public bool GetParameter(string name, ref long result)
        {
            string param = string.Empty;
            if (!GetParameter(name, ref param))
                return false;

            long n;
            if (!long.TryParse(param, out n))
                return false;

            result = n;
            return true;
        }

        public bool GetParameter(string name, ICollection<string> list, char divider)
        {
            string param = string.Empty;
            if (!GetParameter(name, ref param))
                return false;

            foreach (var item in param.Split(divider))
            {
                if (item.Trim().Length > 0)
                    list.Add(item.Trim());
            }

            return true;
        }

        public string GetCookie(string name)
        {
            string cookie = null;
            _cookies.TryGetValue(name.ToLower(), out cookie);
            return cookie;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }

        #region Static methods
        public static int FindHttpHeaderEnd(ref string data, out bool hasRowFeed)
        {
            hasRowFeed = false;

            for (int i = 0; i < data.Length; ++i)
            {
                if (data[i] == '\n')
                {
                    int pos = i;

                    if (i - 1 >= 0 &&
                        data[i - 1] == '\r')
                    {
                        hasRowFeed = true;
                        --pos;
                    }

                    ++i;

                    if (i < data.Length &&
                        hasRowFeed &&
                        data[i] == '\r' &&
                        i + 1 < data.Length &&
                        data[i + 1] == '\n')
                    {
                        // we found \r\n\r\n at pos
                        return pos;
                    }

                    if (i < data.Length &&
                        !hasRowFeed &&
                        data[i] == '\n')
                    {
                        // we found \n\n at pos
                        return pos;
                    }

                    if (i >= data.Length)
                        break;
                }
            }

            return -1;
        }

        public static HttpHeader ParseHeader(ref string header)
        {
            var method = string.Empty;
            var location = string.Empty;
            var version = string.Empty;

            bool foundFirstHeader = false;

            var headers = new Dictionary<string, string>();

            var lines = header.Split('\n');
            for (int i = 0; i < lines.Length; ++i)
            {
                lines[i] = lines[i].Trim();
                if (lines[i].EndsWith("\r"))
                    lines[i] = lines[i].Substring(0, lines[i].Length - 1);

                if (lines[i].Length > 0)
                {
                    if (i == 0)
                    {
                        // GET / HTTP/1.1 ...
                        var data = lines[i].Split(' ');
                        method = data[0];
                        location = data[1];
                        version = data[2];
                        foundFirstHeader = true;
                    }
                    else
                    {
                        // key: value

                        int pos;
                        if ((pos = lines[i].IndexOf(": ")) != -1)
                        {
                            headers.Add(
                                lines[i].Substring(0, pos).ToLower(),
                                lines[i].Substring(pos + 2));
                        }
                    }
                }
            }

            var requestMethod = HttpRequestMethod.Unknown;
            if (string.Compare(method, "GET", true) == 0)
                requestMethod = HttpRequestMethod.Get;
            else if (string.Compare(method, "POST", true) == 0)
                requestMethod = HttpRequestMethod.Post;
            else if (string.Compare(method, "PUT", true) == 0)
                requestMethod = HttpRequestMethod.Put;
            else if (string.Compare(method, "DELETE", true) == 0)
                requestMethod = HttpRequestMethod.Delete;
            else if (string.Compare(method, "OPTIONS", true) == 0)
                requestMethod = HttpRequestMethod.Options;
            else if (string.Compare(method, "HEAD", true) == 0)
                requestMethod = HttpRequestMethod.Head;

            return
                foundFirstHeader ?
                new HttpHeader(requestMethod, new HttpLocation(location), version, headers) :
                null;
        }

        public static void ParseCookieString(string str, IDictionary<string, string> cookies)
        {
            // name=value; name2=value2
            // que=1; Expires=Wed, 09 Jun 2021 10:18:14 GMT <------- NEVER SENT BY BROWSER !

            var key = new StringBuilder(32);
            var value = new StringBuilder(128);
            var sb = key; // used as pointer to current string builder

            for (int i = 0; i < str.Length + 1; ++i)
            {
                if (i >= str.Length || // note i >= str.Length important
                    str[i] == ';')
                {
                    if (key.Length > 0)
                    {
                        if (!cookies.ContainsKey(key.ToString()))
                            cookies.Add(key.ToString().ToLower(), value.ToString());
                    }

                    key.Clear();
                    value.Clear();
                    sb = key;

                    if (i >= str.Length)
                        break;
                }
                else if (str[i] == '=')
                {
                    sb = value;

                }
                else if (!char.IsWhiteSpace(str[i]))
                    sb.Append(str[i]);
            }
        }

        public static void ParseFormEncodedString(string str, IDictionary<string, string> form)
        {
            var key = new StringBuilder(32);
            var value = new StringBuilder(128);
            var sb = key; // used as pointer to current string builder

            for (int i = 0; i < str.Length + 1; ++i)
            {
                if (i >= str.Length || // note i >= str.Length important
                    str[i] == '&')
                {
                    if (key.Length > 0)
                    {
                        if (!form.ContainsKey(key.ToString()))
                            form.Add(key.ToString().ToLower(), value.ToString());
                    }

                    key.Clear();
                    value.Clear();
                    sb = key;

                    if (i >= str.Length)
                        break;
                }
                else if (str[i] == '=')
                {
                    sb = value;

                }
                else if (!char.IsWhiteSpace(str[i]))
                    sb.Append(str[i]);
            }
        }
        #endregion
    }
}
