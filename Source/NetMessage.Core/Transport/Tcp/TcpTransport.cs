using System;
using NetMessage.Core;
using NetMessage.Transport;

namespace NetMessage.Transport.Tcp
{
    class TcpTransport : TransportBase
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

        public override EndpointBase Bind(object hint)
        {
            return new BoundEndpoint((Endpoint)hint);
        }

        public override EndpointBase Connect(object hint)
        {
            return new ConnectEndpoint((Endpoint)hint);
        }
    }
}
