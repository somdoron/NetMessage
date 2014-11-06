using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage
{    
    public enum SocketOption
    {
        Linger = 1,
        SendBuffer =2,
        ReceiveBuffer =3,
        SendTimeout =4,
        ReceiveTimeout =5,
        ReconnectInterval =6,
        ReconnectIntervalMax =7,
        SendPriority =8,
        ReceivePriority =9,        
        Type =13,
        IPV4Only =14,
        SocketName =15,
    }
}
