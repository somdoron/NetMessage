using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage2.Utils
{
    public static class NetworkOrderBitConverter
    {        
        public static int ToInt32(byte[] byteArray, int offset)
        {
            throw new NotImplementedException();
        }

        public static long ToInt64(byte[] byteArray, int offset)
        {
            return BitConverter.ToInt64(byteArray, offset);
        }

        public static byte[] GetBytes(int integer)
        {
            throw new NotImplementedException();
        }

        public static byte[] GetBytes(long integer)
        {
            throw new NotImplementedException();
        }

        public static void PutBytes(byte[] byteArray, int offset, int integer)
        {
            throw new NotImplementedException();
        }

        public static void PutBytes(byte[] byteArray, int offset, long integer)
        {
            
        }
    }
}
