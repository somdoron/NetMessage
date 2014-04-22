using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Core
{    
    public enum PipeStatus
    {        
        Ok = 0,
        Release = 1,        
    }    

    public interface IPipe
    {
        object Data { get; set; }

        PipeStatus Send(Message message);

        PipeStatus Receive(out Message message);

        object GetOption(SocketOption option);
    }
}
