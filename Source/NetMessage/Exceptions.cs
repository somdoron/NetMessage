using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage
{
    public enum NetMessageErrorCode
    {
        ConnectionReset = 1,
        Again,
        AddressInUse,
    }

    public class AgainException : NetMessageException
    {
        public AgainException() : base(NetMessageErrorCode.Again)
        {
            
        }
    }
    
    public class NetMessageException : Exception
    {        
        public NetMessageException(NetMessageErrorCode error)
        {
            ErrorCode = error;
        }

        public NetMessageErrorCode ErrorCode { get; private set; }
    }
}
