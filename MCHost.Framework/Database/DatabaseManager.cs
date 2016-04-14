using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework
{
    public abstract class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void TestConnection()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                connection.Close();
            }
        }

        protected QueryResult Query(string query)
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();

            command.CommandText = query;

            return new QueryResult(connection, command);
        }

        protected QueryResult Query(string query, params object[] args)
        {
            return Query(string.Format(query, args));
        }

        protected int Execute(string query)
        {
            int result = 0;

            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = con.CreateCommand())
                {
                    cmd.CommandText = query;
                    result = cmd.ExecuteNonQuery();
                }
            }

            return result;
        }

        protected int Execute(string query, params object[] args)
        {
            return Execute(string.Format(query, args));
        }
    }

    public class QueryResult : IDisposable
    {
        private SqlConnection _connection = null;
        private SqlCommand _command = null;
        private SqlDataReader _reader = null;

        public QueryResult(SqlConnection connection, SqlCommand command)
        {
            _connection = connection;
            _command = command;
        }

        public void Dispose()
        {
            if (_reader != null)
                _reader.Dispose();

            if (_command != null)
                _command.Dispose();

            if (_connection != null)
            {
                switch (_connection.State)
                {
                    case ConnectionState.Open:
                    case ConnectionState.Fetching:
                    case ConnectionState.Executing:
                        _connection.Close();
                        break;
                }
                _connection.Dispose();
            }
        }

        public QueryResult AddParameter(string name, SqlDbType type, object value)
        {
            _command.Parameters.Add(name, type).Value = value == null ? DBNull.Value : value;
            return this;
        }

        public QueryResult CommandType(CommandType type)
        {
            _command.CommandType = type;
            return this;
        }

        public QueryResult Execute()
        {
            if (_reader != null)
                throw new InvalidOperationException("Cannot call this function right now.");

            _reader = _command.ExecuteReader();
            return this;
        }

        public int ExecuteNonQuery()
        {
            if (_reader != null)
                throw new InvalidOperationException("Cannot call this function right now.");

            return _command.ExecuteNonQuery();
        }

        public bool Read()
        {
            return _reader.Read();
        }

        public bool NextResult()
        {
            return _reader.NextResult();
        }

        public short GetByte(int i, NullValueBehaviour nullValueBehaviour = NullValueBehaviour.ReturnDefaultValue, byte defaultValue = 0)
        {
            if (nullValueBehaviour == NullValueBehaviour.ReturnDefaultValue &&
                _reader.IsDBNull(i))
                return defaultValue;

            return _reader.GetByte(i);
        }

        public short GetInt16(int i, NullValueBehaviour nullValueBehaviour = NullValueBehaviour.ReturnDefaultValue, short defaultValue = 0)
        {
            if (nullValueBehaviour == NullValueBehaviour.ReturnDefaultValue &&
                _reader.IsDBNull(i))
                return defaultValue;

            return _reader.GetInt16(i);
        }

        public int GetInt32(int i, NullValueBehaviour nullValueBehaviour = NullValueBehaviour.ReturnDefaultValue, int defaultValue = 0)
        {
            if (nullValueBehaviour == NullValueBehaviour.ReturnDefaultValue &&
                _reader.IsDBNull(i))
                return defaultValue;

            return _reader.GetInt32(i);
        }

        public long GetInt64(int i, NullValueBehaviour nullValueBehaviour = NullValueBehaviour.ReturnDefaultValue, long defaultValue = 0)
        {
            if (nullValueBehaviour == NullValueBehaviour.ReturnDefaultValue &&
                _reader.IsDBNull(i))
                return defaultValue;

            return _reader.GetInt64(i);
        }

        public float GetFloat(int i, NullValueBehaviour nullValueBehaviour = NullValueBehaviour.ReturnDefaultValue, float defaultValue = 0)
        {
            if (nullValueBehaviour == NullValueBehaviour.ReturnDefaultValue &&
                _reader.IsDBNull(i))
                return defaultValue;

            return (float)_reader.GetDouble(i);
        }

        public double GetDouble(int i, NullValueBehaviour nullValueBehaviour = NullValueBehaviour.ReturnDefaultValue, double defaultValue = 0)
        {
            if (nullValueBehaviour == NullValueBehaviour.ReturnDefaultValue &&
                _reader.IsDBNull(i))
                return defaultValue;

            return _reader.GetDouble(i);
        }

        public string GetString(int i, NullValueBehaviour nullValueBehaviour = NullValueBehaviour.ReturnDefaultValue, string defaultValue = null)
        {
            if (nullValueBehaviour == NullValueBehaviour.ReturnDefaultValue &&
                _reader.IsDBNull(i))
                return defaultValue;

            return _reader.GetString(i);
        }

        public DateTime? GetUtcDateTime(int i, NullValueBehaviour nullValueBehaviour = NullValueBehaviour.ReturnDefaultValue, DateTime? defaultValue = null)
        {
            if (nullValueBehaviour == NullValueBehaviour.ReturnDefaultValue &&
                _reader.IsDBNull(i))
                return defaultValue;

            DateTime date = _reader.GetDateTime(i);
            if (date.Kind == DateTimeKind.Unspecified)
                date = DateTime.SpecifyKind(date, DateTimeKind.Utc);

            return date;
        }

        public bool GetBoolean(int i, NullValueBehaviour nullValueBehaviour = NullValueBehaviour.ReturnDefaultValue, bool defaultValue = false)
        {
            if (nullValueBehaviour == NullValueBehaviour.ReturnDefaultValue &&
                   _reader.IsDBNull(i))
                return defaultValue;

            return _reader.GetBoolean(i);
        }

        public EnumType GetEnum<EnumType>(int i, SqlDbType fieldType, NullValueBehaviour nullValueBehaviour = NullValueBehaviour.ReturnDefaultValue) where EnumType : struct
        {
            if (nullValueBehaviour == NullValueBehaviour.ReturnDefaultValue &&
                   _reader.IsDBNull(i))
                return default(EnumType);

            EnumType value = default(EnumType);
            switch (fieldType)
            {
                case SqlDbType.TinyInt:
                    value = (EnumType)(object)(short)GetByte(i, nullValueBehaviour);
                    break;
                case SqlDbType.SmallInt:
                    value = (EnumType)(object)GetInt16(i, nullValueBehaviour);
                    break;
                case SqlDbType.Int:
                    value = (EnumType)(object)GetInt32(i, nullValueBehaviour);
                    break;
                case SqlDbType.BigInt:
                    value = (EnumType)(object)GetInt64(i, nullValueBehaviour);
                    break;
            }

            return value;
        }

        public byte? GetNullByte(int i)
        {
            if (_reader.IsDBNull(i))
                return null;

            return _reader.GetByte(i);
        }

        public short? GetNullInt16(int i)
        {
            if (_reader.IsDBNull(i))
                return null;

            return _reader.GetInt16(i);
        }

        public int? GetNullInt32(int i)
        {
            if (_reader.IsDBNull(i))
                return null;

            return _reader.GetInt32(i);
        }

        public long? GetNullInt64(int i)
        {
            if (_reader.IsDBNull(i))
                return null;

            return _reader.GetInt64(i);
        }

        public float? GetNullFloat(int i)
        {
            if (_reader.IsDBNull(i))
                return null;

            return (float)_reader.GetFloat(i);
        }

        public double? GetNullDouble(int i)
        {
            if (_reader.IsDBNull(i))
                return null;

            return _reader.GetDouble(i);
        }
    }

    public enum NullValueBehaviour
    {
        ThrowException,
        ReturnDefaultValue
    }
}
