using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Network
{
    public interface IDataWriter
    {
        void Write(byte[] buffer, int offset, int length);
        void Write(byte value);
        void Write(bool value);
        void Write(short value);
        void Write(int value);
        void Write(long value);
        void Write(string value);
    }

    public interface IDataReader
    {
        void ReadBytes(byte[] buffer, int offsetInBuffer, int bytesToRead);
        byte ReadByte();
        bool ReadBoolean();
        short ReadInt16();
        int ReadInt32();
        long ReadInt64();
        string ReadString();
    }

    public class DataBuffer : IDataWriter, IDataReader
    {
        public const int MaxSize = 1048576; // 1 MB

        private byte[] _buffer = new byte[1024];
        private int _offset = 0;
        private int _length = 0;
        private bool _isReadOnly = false;

        private readonly Encoding _encoding = Encoding.GetEncoding(1252);

        /// <summary>
        /// Gets the internal buffer for direct access.
        /// </summary>
        public byte[] InternalBuffer { get { return _buffer; } }

        /// <summary>
        /// Gets a value indicating whether the DataBuffer is in read-only mode.
        /// </summary>
        public bool IsReadOnly { get { return _isReadOnly; } }

        /// <summary>
        /// Gets or sets the offset within the internal buffer for reading.
        /// <para>The offset must be between 0 and DataBuffer.Length</para>
        /// </summary>
        public int Offset
        {
            get { return _offset; }
            set
            {
                if (value < 0 ||
                    value > _length)
                    throw new ArgumentOutOfRangeException("Offset", "The offset must be within the range of 0 and DataBuffer.Length (inclusive). Value " + value + " in " + _length);

                _offset = value;
            }
        }

        /// <summary>
        /// Gets the amount of bytes contained in the internal buffer.
        /// </summary>
        public int Length { get { return _length; } }

        /// <summary>
        /// Allocates an internal buffer to use for reading or writing.
        /// </summary>
        public DataBuffer()
        {
        }

        /// <summary>
        /// Constructs a DataBuffer for read-only of an existing buffer.
        /// </summary>
        /// <param name="buffer">Read-only buffer</param>
        /// <param name="length">Length of data within buffer</param>
        public DataBuffer(byte[] buffer, int length)
        {
            _buffer = buffer;
            _length = length;
            _isReadOnly = true;
        }

        /// <summary>
        /// Resets the reading offset and the length (if not in read-only mode) of the buffer.
        /// </summary>
        public void Reset()
        {
            _offset = 0;
            if (!_isReadOnly)
                _length = 0;
        }

        public void Remove(int bytes)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("Cannot modify a read-only buffer.");

            if (bytes > _length)
                throw new ArgumentOutOfRangeException("Cannot remove more bytes than what is contained within the DataBuffer.");

            if (bytes == _length)
            {
                Reset();
                return;
            }

            Buffer.BlockCopy(_buffer, bytes, _buffer, 0, _length - bytes);
            _offset -= bytes;
            _length -= bytes;
            if (_offset < 0)
                _offset = 0;
        }

        private void CheckSize(int add)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("Cannot modify a read-only buffer.");

            if (_offset + add < _buffer.Length) // dont need to do the checks below
                return;

            var bufferSize = _buffer.Length;

            while (_offset + add > bufferSize)
                bufferSize *= 2;

            if (bufferSize > MaxSize)
                bufferSize = MaxSize;

            if (_offset + add >= bufferSize)
                throw new OverflowException($"Cannot contain more than {MaxSize} bytes of data.");

            if (bufferSize > _buffer.Length)
                Array.Resize(ref _buffer, bufferSize);
        }

        #region Read types
        public void ReadBytes(byte[] buffer, int offsetInBuffer, int bytesToRead)
        {
            if (_offset + bytesToRead > _length)
                throw new EndOfBufferException();

            Buffer.BlockCopy(_buffer, _offset, buffer, offsetInBuffer, bytesToRead);
            _offset += bytesToRead;
        }

        public byte ReadByte()
        {
            if (_offset + 1 > _length)
                throw new EndOfBufferException();

            return _buffer[_offset++];
        }

        public bool ReadBoolean()
        {
            return ReadByte() != 0;
        }

        public short ReadInt16()
        {
            if (_offset + 2 > _length)
                throw new EndOfBufferException();

            return (short)(
                _buffer[_offset++] |
                _buffer[_offset++] << 8);
        }

        public int ReadInt32()
        {
            if (_offset + 4 > _length)
                throw new EndOfBufferException();

            return
                _buffer[_offset++] |
                _buffer[_offset++] << 8 |
                _buffer[_offset++] << 16 |
                _buffer[_offset++] << 24;
        }

        public long ReadInt64()
        {
            if (_offset + 8 > _length)
                throw new EndOfBufferException();

            return
                _buffer[_offset++] |
                _buffer[_offset++] << 8 |
                _buffer[_offset++] << 16 |
                _buffer[_offset++] << 24 |
                _buffer[_offset++] << 32 |
                _buffer[_offset++] << 40 |
                _buffer[_offset++] << 48 |
                _buffer[_offset++] << 56;
        }

        public string ReadString()
        {
            var length = ReadInt32();
            var buffer = new byte[length];
            ReadBytes(buffer, 0, length);
            return _encoding.GetString(buffer, 0, length);
        }
        #endregion

        #region Write types
        public void Write(byte[] buffer, int offset, int length)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("Cannot modify a read-only buffer.");

            CheckSize(length);
            Buffer.BlockCopy(buffer, offset, _buffer, _offset, length);
            _offset += length;
            if (_offset > _length)
                _length = _offset;
        }

        public void Write(byte value)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("Cannot modify a read-only buffer.");

            CheckSize(1);
            _buffer[_offset++] = value;
            if (_offset > _length)
                _length = _offset;
        }

        public void Write(bool value)
        {
            Write((byte)(value ? 1 : 0));
        }

        public void Write(short value)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("Cannot modify a read-only buffer.");

            CheckSize(2);
            _buffer[_offset++] = (byte)value;
            _buffer[_offset++] = (byte)(value >> 8);
            if (_offset > _length)
                _length = _offset;
        }

        public void Write(int value)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("Cannot modify a read-only buffer.");

            CheckSize(4);
            _buffer[_offset++] = (byte)value;
            _buffer[_offset++] = (byte)(value >> 8);
            _buffer[_offset++] = (byte)(value >> 16);
            _buffer[_offset++] = (byte)(value >> 24);
            if (_offset > _length)
                _length = _offset;
        }

        public void Write(long value)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("Cannot modify a read-only buffer.");

            CheckSize(8);
            _buffer[_offset++] = (byte)value;
            _buffer[_offset++] = (byte)(value >> 8);
            _buffer[_offset++] = (byte)(value >> 16);
            _buffer[_offset++] = (byte)(value >> 24);
            _buffer[_offset++] = (byte)(value >> 32);
            _buffer[_offset++] = (byte)(value >> 40);
            _buffer[_offset++] = (byte)(value >> 48);
            _buffer[_offset++] = (byte)(value >> 56);
            if (_offset > _length)
                _length = _offset;
        }

        public void Write(string value)
        {
            var bytes = _encoding.GetBytes(value);

            Write(bytes.Length);
            Write(bytes, 0, bytes.Length);
        }
        #endregion

        public static DataBuffer FromString(string content, Encoding encoding)
        {
            var bytes = encoding.GetBytes(content);
            var buffer = new DataBuffer();
            buffer.Write(bytes, 0, bytes.Length);
            return buffer;
        }

        public static DataBuffer FromString(string content)
        {
            return FromString(content, Encoding.GetEncoding(1252));
        }
    }

    public class EndOfBufferException : Exception
    {
        public EndOfBufferException() :
            base("An attempt was made to read more bytes than what exists in a DataBuffer.")
        {
        }
    }
}
