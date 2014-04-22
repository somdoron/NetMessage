using NetMessage.Core.Core;
using NetMessage.Core.Transport;

namespace NetMessage.NetMQ.InProc
{
    public class InProcTransport : Core.Transport.Transport<NetMQMessage>
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

        public override EndpointBase<NetMQMessage> Bind(object hint)
        {
            return new BoundEndpoint((Endpoint<NetMQMessage>)hint);
        }

        public override EndpointBase<NetMQMessage> Connect(object hint)
        {
            return new ConnectEndpoint((Endpoint<NetMQMessage>)hint);
        }
    }
}
