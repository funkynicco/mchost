using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework
{
    public class DataSerializer
    {
        private readonly HashSet<char> _charactersToEscape = new HashSet<char>();
        private readonly StringBuilder _sb = new StringBuilder();

        public DataSerializer()
        {
        }

        public DataSerializer(IEnumerable<char> charactersToEscape)
        {
            AddEscapeCharacters(charactersToEscape);
        }

        public DataSerializer(params char[] charactersToEscape)
        {
            AddEscapeCharacters((IEnumerable<char>)charactersToEscape);
        }

        public override string ToString()
        {
            return _sb.ToString();
        }

        public void AddEscapeCharacters(IEnumerable<char> charactersToEscape)
        {
            foreach (var c in charactersToEscape)
                _charactersToEscape.Add(c);
        }

        public void AddEscapeCharacters(params char[] charactersToEscape)
        {
            AddEscapeCharacters((IEnumerable<char>)charactersToEscape);
        }

        public void Add(string value)
        {
            if (_sb.Length > 0)
                _sb.Append('/');

            foreach (char c in value)
            {
                if (c == '/' ||
                    _charactersToEscape.Contains(c))
                    _sb.Append($"&#{(byte)c};");
                else
                    _sb.Append(c);
            }
        }

        public void Add(bool value)
        {
            Add(value ? "1" : "0");
        }

        public void Add(int value)
        {
            Add(value.ToString());
        }

        public void Add(long value)
        {
            Add(value.ToString());
        }

        public void Add(float value)
        {
            Add(value.ToString());
        }

        public void Add(double value)
        {
            Add(value.ToString());
        }
    }

    public class DataDeserializer
    {
        private readonly string[] _data;
        private int _index = 0;

        public int Count { get { return _data.Length; } }
        public bool IsEndOfFile { get { return _index >= _data.Length; } }

        public DataDeserializer(string data)
        {
            _data = data.Split('/');
            for (int i = 0; i < _data.Length; ++i)
            {
                _data[i] = Utilities.DecodeAsciiString(_data[i]);
            }
        }

        public void Reset()
        {
            _index = 0;
        }

        public string GetString()
        {
            return _data[_index++];
        }

        public bool GetBoolean()
        {
            var val = GetString();
            switch (val)
            {
                case "1":
                    return true;
                case "0":
                    return false;
            }

            throw new FormatException($"The parameter '{val}' is not a valid boolean value.");
        }

        public int GetInt32()
        {
            return int.Parse(GetString());
        }

        public long GetInt64()
        {
            return long.Parse(GetString());
        }
    
        public float GetFloat()
        {
            return float.Parse(GetString());
        }

        public double GetDouble()
        {
            return double.Parse(GetString());
        }
    }
}
