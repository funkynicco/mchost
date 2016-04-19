using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Network
{
    public class DataBuffer
    {
        public int MaxSize = 1048576; // 1 MB

        private byte[] _buffer = new byte[1024];
        private int _offset = 0;
        private int _length = 0;

        public void CheckSize(int add)
        {
            if (_length + add >= MaxSize)
                throw new OverflowException($"Cannot contain more than {MaxSize} bytes of data.");
        }

        public void Append(byte[] buffer, int offset, int length)
        {
            Buffer.BlockCopy(buffer, offset, _buffer, _offset, length);
        }
    }
}
