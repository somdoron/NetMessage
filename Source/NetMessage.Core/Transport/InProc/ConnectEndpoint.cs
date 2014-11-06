using System.Diagnostics;
using NetMessage.Core;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Core;
using NetMessage.Core.Transport;

namespace NetMessage.NetMQ.InProc
{
    class ConnectEndpoint : EndpointBase
    {
        enum State
        {
            Idle=1,
            Disconnected=2,
            Active=3,
            Stopping=4
        }

        private const int ConnectAction = 1;       

        private State m_state;        

        private Session m_session;

        public ConnectEndpoint(Endpoint endpoint)
            : base(endpoint)
        {            
            m_state = State.Idle;
            m_session = new Session(this, this);

            SocketType = (int)endpoint.GetOption(SocketOption.Type);

            base.StartStateMachine();
            InProcSystem.Instance.Connect(this, OnConnect);
        }

        public int SocketType { get; private set; }        

        private void OnConnect(BoundEndpoint peer)
        {           
            Debug.Assert(m_state ==State.Disconnected);

            m_session.Connect(peer);
            Action(ConnectAction);
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == StateMachine.ActionSourceId && type == StopAction)
            {
                // First, unregister the endpoint from the global repository of inproc
                // endpoints. This way, new connections cannot be created anymore.

                InProcSystem.Instance.Disconnect(this);
                m_session.Stop();
                m_state = State.Stopping;
            }

            if (m_state == State.Stopping)
            {
                if (m_session.IsIdle)
                {
                    m_state = State.Idle;
                    base.StoppedNoEvent();
                    base.Stopped();
                }
            }
            else
            {
                // throw bad state
            }
        }

        protected override void Handle(int sourceId, int type, StateMachine source)
        {
            switch (m_state)
            {
                case State.Idle:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case StateMachine.StartAction:
                                    m_state = State.Disconnected;
                                    break;
                                default:
                                    // TODO: throw bad action
                                    break;
                            }
                            break;
                        default:
                            // TODO:throw bad source
                            break;
                    }
                    break;
                case State.Disconnected:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case ConnectAction:
                                    m_state = State.Active;
                                    break;
                                default:
                                    // TODO: throw bad action
                                    break;
                            }
                            break;
                        case Session.PeerSourceId:
                            switch (type)
                            {
                                case Session.ConnectEvent:
                                    Session session = (Session) source;
                                    m_session.Accept(session);
                                    m_state = State.Active;
                                    break;
                                default:
                                    // TODO:throw bad action
                                    break;
                            }
                            break;
                        default:
                            // TODO:bad source
                            break;
                    }
                    break;   
                    case State.Active:
                        // TODO:bad source
                    break;
                default:
                    //  TODO: throw bad state
                    break;   
            }
        }
    }
}
