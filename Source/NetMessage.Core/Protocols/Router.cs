using System;
using System.Collections.Generic;
using System.Diagnostics;
using NetMessage.Core;
using NetMessage.Core;
using NetMessage.Protocols.Utils;

namespace NetMessage.Protocols
{
    class Router : SocketBase
    {
        public class RouterSocketType : SocketType
        {
            public RouterSocketType()
                : base(SocketTypes.Router, SocketTypeFlags.None)
            {

            }

            public override SocketBase Create(object hint)
            {
                return new Router(hint);
            }

            public override bool IsPeer(int socketType)
            {
                return socketType == SocketTypes.Router ||
                      socketType == SocketTypes.Request ||
                      socketType == SocketTypes.Dealer;
            }
        }

        class Data
        {
            public UInt32 Key { get; set; }
            public IPipe Pipe { get; set; }
            public FairQueuing.Data FairQueueingData { get; set; }

            public bool HasOut { get; set; }
        }

        private UInt32 m_nextKey;
        private Dictionary<uint, Data> m_outpipes;

        private FairQueuing m_inpipes;

        public Router(object hint)
            : base(hint)
        {
            m_outpipes = new Dictionary<uint, Data>();
            m_inpipes = new FairQueuing();

            Random random = new Random();

            // Start assigning keys beginning with a random number. This way there
            // are no key clashes even if the executable is re-started.
            m_nextKey = (uint)random.Next();
        }

        public override void Dispose()
        {
            m_inpipes = null;
            m_outpipes = null;
            base.Dispose();
        }

        protected internal override void Add(IPipe pipe)
        {
            int receivePriority = (int)pipe.GetOption(SocketOption.ReceivePriority);

            Data data = new Data();
            data.Key = m_nextKey;
            data.FairQueueingData = m_inpipes.Add(pipe, receivePriority);
            data.HasOut = false;
            data.Pipe = pipe;
            m_outpipes.Add(data.Key, data);

            pipe.Data = data;
            m_nextKey++;
        }

        protected internal override void Remove(IPipe pipe)
        {
            Data data = (Data)pipe.Data;

            m_inpipes.Remove(data.FairQueueingData);
            m_outpipes.Remove(data.Key);

            pipe.Data = null;
        }

        protected internal override void In(IPipe pipe)
        {
            Data data = (Data)pipe.Data;
            m_inpipes.In(data.FairQueueingData);
        }

        protected internal override void Out(IPipe pipe)
        {
            Data data = (Data)pipe.Data;
            data.HasOut = true;
        }

        protected internal override SocketEvents Events
        {
            get
            {
                return (m_inpipes.CanReceive ? SocketEvents.In : SocketEvents.None) |
                    SocketEvents.Out;
            }
        }

        protected internal override SendReceiveResult Send(Message message)
        {
            // If we have malformed message (prefix with no subsequent message)
            //  then just silently ignore it.
            if (message.FrameCount < 1)
            {
                return SendReceiveResult.Ok;
            }

            Frame keyFrame = message.Pop();

            // We treat invalid peer ID as if the peer was non-existent.           
            if (keyFrame.MessageSize < sizeof(uint))
            {
                // TODO: add the mandatory attribute as in ZeroMQ
                return SendReceiveResult.Ok;
            }

            UInt32 key = BitConverter.ToUInt32(keyFrame.Buffer.Array, keyFrame.Buffer.Offset);

            Data currentOut;

            if (!m_outpipes.TryGetValue(key, out currentOut))
            {
                // TODO: if mandatory throw exception
                return SendReceiveResult.Ok;
            }

            if (!currentOut.HasOut)
            {
                // TODO: if mandatory return false
                return SendReceiveResult.Ok;
            }

            var pipeStatus = currentOut.Pipe.Send(message);

            if (pipeStatus == PipeStatus.Release)
            {
                currentOut.HasOut = false;
            }

            return SendReceiveResult.Ok;
        }

        protected internal override SendReceiveResult Receive(out Message message)
        {
            IPipe pipe;

            var result = m_inpipes.Receive(out message, out pipe);

            if (result == SendReceiveResult.ShouldTryAgain)
            {
                message = null;
                return SendReceiveResult.ShouldTryAgain;
            }

            Debug.Assert(pipe != null);

            Data data = (Data)pipe.Data;

            byte[] key = BitConverter.GetBytes(data.Key);

            message.Push(key);

            return SendReceiveResult.Ok;
        }

        protected internal override void SetOption(int option, object value)
        {
            throw new NotSupportedException();
        }
    }
}
