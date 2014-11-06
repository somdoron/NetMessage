using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Transport.Utils
{
    public static class BufferUtility
    {
        public static void WriteLong(byte[] buffer, int offset, long value)
        {
            buffer[offset] = (byte)(((value) >> 56) & 0xff);
            buffer[offset + 1] = (byte)(((value) >> 48) & 0xff);
            buffer[offset + 2] = (byte)(((value) >> 40) & 0xff);
            buffer[offset + 3] = (byte)(((value) >> 32) & 0xff);
            buffer[offset + 4] = (byte)(((value) >> 24) & 0xff);
            buffer[offset + 5] = (byte)(((value) >> 16) & 0xff);
            buffer[offset + 6] = (byte)(((value) >> 8) & 0xff);
            buffer[offset + 7] = (byte)(value & 0xff);
        }

        public static long ReadLong(byte[] bytes, int offset)
        {
            return (((long)bytes[offset]) << 56) |
                    (((long)bytes[offset + 1]) << 48) |
                    (((long)bytes[offset + 2]) << 40) |
                    (((long)bytes[offset + 3]) << 32) |
                    (((long)bytes[offset + 4]) << 24) |
                    (((long)bytes[offset + 5]) << 16) |
                    (((long)bytes[offset + 6]) << 8) |
                    ((long)bytes[offset + 7]);
        }
    }
}
