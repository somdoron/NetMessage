using System;
using NetMessage.Core.Core;
using NetMessage.Core.Transport;
using NetMessage.Core.Transport.Tcp;

namespace NetMessage.NetMQ.Tcp
{
    public class TcpTransport : Transport<NetMQMessage>
    {
        public override OptionSet GetOptionSet()
        {
            return new TcpOptionSet();
        }

        public override string Name
        {
            get { return "tcp"; }
        }

        public override void Dispose()
        {
            
        }

        public override EndpointBase<NetMQMessage> Bind(object hint)
        {
            return new BoundEndpoint((Endpoint<NetMQMessage>)hint);
        }

        public override EndpointBase<NetMQMessage> Connect(object hint)
        {
            throw new NotImplementedException();
        }
    }
}
