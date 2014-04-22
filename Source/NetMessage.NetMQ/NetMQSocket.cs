using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core;
using NetMessage.Core.Core;

namespace NetMessage.NetMQ
{
    public class NetMQSocket : Socket<NetMQMessage>
    {
        internal NetMQSocket(SocketType<NetMQMessage> socketType) : base(socketType)
        {
        }

        protected override Core.Transport.Transport<NetMQMessage> GetTransport(string name)
        {
            return Global.GetTransport(name);
        }
    }
}
