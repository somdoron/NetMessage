using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Core.Core
{    
    public enum PipeStatus
    {        
        Ok = 0,
        Release = 1,        
    }    

    public interface IPipe<T> where T: MessageBase
    {
        object Data { get; set; }

        PipeStatus Send(T message);

        PipeStatus Receive(out T message);

        object GetOption(SocketOption option);
    }
}
