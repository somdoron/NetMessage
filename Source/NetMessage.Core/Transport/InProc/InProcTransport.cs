using NetMessage.Core;
using NetMessage.Transport;

namespace NetMessage.Transport.InProc
{
    class InProcTransport : TransportBase
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
