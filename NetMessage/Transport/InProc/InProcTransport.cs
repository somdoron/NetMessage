using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core;

namespace NetMessage.Transport.InProc
{
    public class InProcTransport : Transport
    {
        public InProcTransport()
        {
            InProcSystem.Instance.Init();
        }

        public override void Dispose()
        {
            InProcSystem.Instance.Dispose();
        }

        public override OptionSet GetOptionSet()
        {
            return null;
        }

        public override string Name
        {
            get { return "inproc"; }
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
