﻿using System;
using NetMessage.Core.Core;
using NetMessage.Core.Transport;
using NetMessage.Core.Transport.Tcp;

namespace NetMessage.NetMQ.Tcp
{
    class TcpTransport : Transport
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