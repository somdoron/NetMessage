using System;
using NetMessage.Core;
using NetMessage.Protocols.Utils;

namespace NetMessage.Protocols
{
    class Dealer : SocketBase
    {
        public class DealerSocketType : SocketType
        {
            public DealerSocketType()
                : base(SocketTypes.Dealer, SocketTypeFlags.None)
            {

            }

            public override SocketBase Create(object hint)
            {
                return new Dealer(hint);
            }

            public override bool IsPeer(int socketType)
            {
                return socketType == SocketTypes.Router ||
                       socketType == SocketTypes.Response ||
                       socketType == SocketTypes.Dealer;
            }
        }

        public class Data
        {
            public Data()
            {
            }

            public FairQueuing.Data FairQueueingData { get; set; }
            public LoadBalancer.Data LoadBalancerData { get; set; }
        }

        private FairQueuing m_fairQueueing;
        private LoadBalancer m_loadBalancer;

        public Dealer(object hint)
            : base(hint)
        {
            m_loadBalancer = new LoadBalancer();
            m_fairQueueing = new FairQueuing();
        }

        public override void Dispose()
        {
            m_loadBalancer = null;
            m_fairQueueing = null;
            base.Dispose();
        }

        protected internal override void Add(IPipe pipe)
        {
            int sendPriority = (int)pipe.GetOption(SocketOption.SendPriority);
            int receivePriority = (int)pipe.GetOption(SocketOption.ReceivePriority);

            Data data = new Data();
            pipe.Data = data;

            data.LoadBalancerData = m_loadBalancer.Add(pipe, sendPriority);
            data.FairQueueingData = m_fairQueueing.Add(pipe, receivePriority);
        }

        protected internal override void Remove(IPipe pipe)
        {
            Data data = (Data)pipe.Data;

            m_loadBalancer.Remove(data.LoadBalancerData);
            m_fairQueueing.Remove(data.FairQueueingData);

            pipe.Data = null;
        }

        protected internal override void In(IPipe pipe)
        {
            Data data = (Data)pipe.Data;
            m_fairQueueing.In(data.FairQueueingData);
        }

        protected internal override void Out(IPipe pipe)
        {
            Data data = (Data)pipe.Data;
            m_loadBalancer.Out(data.LoadBalancerData);
        }

        protected internal override SocketEvents Events
        {
            get
            {
                return (m_fairQueueing.CanReceive ? SocketEvents.In : SocketEvents.None) |
                       (m_loadBalancer.CanSend ? SocketEvents.Out : SocketEvents.None);
            }
        }

        protected internal override SendReceiveResult Send(Message message)
        {
            return m_loadBalancer.Send(message);
        }

        protected internal override SendReceiveResult Receive(out Message message)
        {
            return m_fairQueueing.Receive(out message);
        }

        protected internal override void SetOption(int option, object value)
        {
            throw new NotSupportedException();
        }
    }
}
