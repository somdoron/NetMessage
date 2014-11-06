using System;
using System.Diagnostics;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Core;

namespace NetMessage.Core.Transport
{
    abstract class PipeBase : StateMachine, IPipe
    {
        public const int InEvent = 33987;
        public const int OutEvent = 33988;

        enum State
        {
            Idle = 1, Active, Failed
        }

        enum InState
        {
            Deactivated = 0, Idle, Receiving, Received, Async
        }

        enum OutState
        {
            Deactivated = 0, Idle, Sending, Sent, Async
        }

        private State m_state;
        private InState m_inState;
        private OutState m_outState;

        private Socket m_socket;

        private StateMachineEvent m_inEvent;
        private StateMachineEvent m_outEvent;
        private EndpointOptions m_options;

        public PipeBase(EndpointBase endpointBase)
            : base(0, endpointBase.Endpoint.Socket)
        {
            m_state = State.Idle;
            m_inState = InState.Deactivated;
            m_outState = OutState.Deactivated;

            m_socket = endpointBase.Endpoint.Socket;
            m_options = endpointBase.Endpoint.Options.Clone();

            m_inEvent = new StateMachineEvent();
            m_outEvent = new StateMachineEvent();
        }

        public object Data { get; set; }

        public override void Dispose()
        {
            Debug.Assert(m_state == State.Idle);

            m_inEvent.Dispose();
            m_outEvent.Dispose();
            base.Dispose();
        }

        public void Start()
        {
            Debug.Assert(m_state == State.Idle);

            // base.Start();   

            m_state = State.Active;
            m_inState = InState.Async;
            m_outState = OutState.Idle;

            try
            {
                m_socket.Add(this);
            }
            catch (Exception)
            {
                m_state = State.Failed;
                throw;
            }

            if (m_socket != null)
            {
                base.Raise(m_outEvent, OutEvent);
            }
        }

        public void Stop()
        {
            if (m_state == State.Active)
            {
                m_socket.Remove(this);
            }
            m_state = State.Idle;
        }

        public void OnReceived()
        {
            if (m_inState == InState.Receiving)
            {
                m_inState = InState.Received;
            }
            else
            {
                Debug.Assert(m_inState == InState.Async);

                m_inState = InState.Idle;

                if (m_socket != null)
                {
                    base.Raise(m_inEvent, InEvent);
                }
            }
        }

        public void OnSent()
        {
            if (m_outState == OutState.Sending)
            {
                m_outState = OutState.Sent;
            }
            else
            {
                Debug.Assert(m_outState == OutState.Async);

                m_outState = OutState.Idle;

                if (m_socket != null)
                {
                    base.Raise(m_outEvent, OutEvent);
                }
            }
        }

        public object GetOption(SocketOption option)
        {
            switch (option)
            {
                case SocketOption.SendPriority:
                    return m_options.SendPriority;
                case SocketOption.ReceivePriority:
                    return m_options.ReceivePriority;
                case SocketOption.IPV4Only:
                    return m_options.IP4Only;
            }

            return m_socket.GetOptionInner(option);
        }

        public bool IsPeer(int socketType)
        {
            return m_socket.IsPeer(socketType);
        }

        public PipeStatus Send(Message message)
        {
            Debug.Assert(m_outState == OutState.Idle);

            m_outState = OutState.Sending;

            var pipeStatus = SendInner(message);

            if (m_outState == OutState.Sent)
            {
                m_outState = OutState.Idle;
                return pipeStatus;
            }

            Debug.Assert(m_outState == OutState.Sending);
            m_outState = OutState.Async;
            return PipeStatus.Release;
        }

        public PipeStatus Receive(out Message message)
        {
            Debug.Assert(m_inState == InState.Idle);
            m_inState = InState.Receiving;
            var result = ReceiveInner(out message);

            if (m_inState == InState.Received)
            {
                m_inState = InState.Idle;
                return result;
            }

            Debug.Assert(m_inState == InState.Receiving);
            m_inState = InState.Async;
            return PipeStatus.Release;
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {

        }

        protected override void Handle(int sourceId, int type, StateMachine source)
        {

        }

        protected abstract PipeStatus SendInner(Message message);

        protected abstract PipeStatus ReceiveInner(out Message message);
    }
}
