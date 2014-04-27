using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using NetMessage.Core;
using NetMessage.Core.AsyncIO;

namespace NetMessage.NetMQ.Tcp
{
    public class AcceptConnection : StateMachine
    {
        public const int AcceptedEvent = 34231;
        public const int ErrorEvent = 34232;
        public const int StoppedEvent = 34233;

        enum State
        {
            Idle = 1,
            Accepting = 2,
            Active = 3,
            StoppingSession = 4,
            StoppingUSocket = 5,
            Done = 6,
            StoppingSessionFinal = 7,
            Stopping = 8
        }

        private const int USocketSourceId = 1;
        private const int SessionSourceId = 2;
        private const int ListenerSourceId = 3;

        private State m_state;
        private USocket m_usocket;

        private USocket m_listener;
        private StateMachine m_listenerOwner;
        private int m_listenerOwnerSourceId;

        private Session m_session;

        private StateMachineEvent m_acceptedEvent;
        private StateMachineEvent m_doneEvent;

        private BoundEndpoint m_endpoint;

        public AcceptConnection(BoundEndpoint owner, int sourceId)
            : base(sourceId, owner)
        {
            m_state = State.Idle;
            m_endpoint = owner;

            m_usocket = new USocket(USocketSourceId, this);
            m_listener = null;
            m_listenerOwner = null;
            m_listenerOwnerSourceId = -1;

            m_session = new Session(SessionSourceId, m_endpoint, this);
            m_acceptedEvent = new StateMachineEvent();
            m_doneEvent = new StateMachineEvent();
        }

        public bool IsIdle
        {
            get
            {
                return base.IsStateMachineIdle;
            }
        }



        public override void Dispose()
        {
            Debug.Assert(m_state == State.Idle);

            m_acceptedEvent.Dispose();
            m_doneEvent.Dispose();

            m_session.Dispose();
            m_usocket.Dispose();

            base.Dispose();
        }

        public void Start(USocket listener)
        {
            Debug.Assert(m_state == State.Idle);

            m_listener = listener;
            m_listenerOwnerSourceId = ListenerSourceId;
            m_listenerOwner = this;
            m_listener.SwapOwner(ref m_listenerOwner, ref m_listenerOwnerSourceId);

            StartStateMachine();
        }

        public void Stop()
        {
            StopStateMachine();
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == StateMachine.ActionSourceId && type == StopAction)
            {
                if (!m_session.IsIdle)
                {
                    m_session.Stop();
                }

                m_state = State.StoppingSessionFinal;
            }

            if (m_state == State.StoppingSessionFinal)
            {
                if (!m_session.IsIdle)
                {
                    return;
                }

                m_usocket.Stop();
                m_state = State.Stopping;
            }

            if (m_state == State.Stopping)
            {
                if (!m_usocket.IsIdle)
                {
                    return;
                }

                if (m_listener != null)
                {
                    m_listener.SwapOwner(ref m_listenerOwner, ref m_listenerOwnerSourceId);
                    m_listener = null;
                    m_listenerOwner = null;
                    m_listenerOwnerSourceId = -1;
                }

                m_state = State.Idle;
                Stopped(StoppedEvent);
            }

            // TODO: throw bad action
        }

        protected override void Handle(int sourceId, int type, StateMachine source)
        {
            switch (m_state)
            {
                case State.Idle:
                    switch (sourceId)
                    {
                        case StartAction:
                            m_usocket.Accept(m_listener);
                            m_state = State.Accepting;
                            break;
                    }
                    break;
                case State.Accepting:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.AcceptedEvent:
                                    int sendBuffer = (int)m_endpoint.GetOption(SocketOption.SendBuffer);
                                    int receieBuffer = (int)m_endpoint.GetOption(SocketOption.ReceiveBuffer);

                                    // TODO: use direct properties instead, more .net style
                                    m_usocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, sendBuffer);
                                    m_usocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, receieBuffer);
                                    m_usocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, 1);

                                    m_listener.SwapOwner(ref m_listenerOwner, ref m_listenerOwnerSourceId);
                                    m_listener = null;
                                    m_listenerOwner = null;
                                    m_listenerOwnerSourceId = -1;

                                    Raise(m_acceptedEvent, AcceptedEvent);

                                    m_usocket.Activate();
                                    m_session.Start(m_usocket);

                                    m_state = State.Active;

                                    break;
                            }
                            break;                        
                    }
                    break;
                case State.Active:
                    switch (sourceId)
                    {
                        case SessionSourceId:
                            switch (type)
                            {
                                case Session.ErrorEvent:
                                    m_session.Stop();
                                    m_state = State.StoppingSession;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.StoppingSession:
                    switch (sourceId)
                    {
                        case SessionSourceId:
                            switch (type)
                            {
                                case USocket.ShutdownEvent:
                                    break;
                                case Session.StoppedEvent:
                                    m_usocket.Stop();
                                    m_state = State.StoppingUSocket;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.StoppingUSocket:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ShutdownEvent:
                                    break;
                                case USocket.StoppedEvent:
                                    Raise(m_doneEvent, ErrorEvent);
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }
    }
}
