using System;
using System.Collections.Generic;
using System.Diagnostics;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Core;

namespace NetMessage.Core.Transport.InProc
{
    public class BoundEndpoint : EndpointBase
    {
        enum State
        {
           Idle=1,Active=2, Stopping =3 
        }        

        private State m_state;
        private List<Session> m_sessions;        

        public BoundEndpoint(Endpoint endpoint) : base(endpoint)
        {            
            m_state = State.Idle;
            m_sessions = new List<Session>();
            Connects = 0;

            SocketType = (int)endpoint.GetOption(SocketOption.Type);

            base.StartStateMachine();

            try
            {
                InProcSystem.Instance.Bind(this, OnConnect);
            }
            catch (Exception)
            {
                m_state = State.Idle;
                
                throw;
            }            
        }

        public int SocketType { get; private set; }

        public int Connects { get; set; }

        private void OnConnect(ConnectEndpoint peer)
        {            
            Session session = new Session(this, this);
            m_sessions.Add(session);
            session.Connect(peer);
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == StateMachine.ActionSourceId && type == StateMachine.StopAction)
            {
                InProcSystem.Instance.Unbind(this);                               

                if (m_sessions.Count != 0)
                {
                    foreach (Session session in m_sessions)
                    {
                        session.Stop();
                    }

                    m_state = State.Stopping;    
                }
                else
                {
                    m_state = State.Idle;
                    base.StoppedNoEvent();
                    base.Stopped();
                }
            }
            else if (m_state == State.Stopping)
            {
                Debug.Assert(sourceId == Session.SourceId && type == Session.StoppedEvent);
                Session session = (Session) source;

                m_sessions.Remove(session);
                session.Dispose();

                if (m_sessions.Count == 0)
                {
                    m_state = State.Idle;
                    base.StoppedNoEvent();
                    base.Stopped();
                }
            }

            // TODO: throw bad state
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
                                    m_state = State.Active;
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
                case State.Active:
                    switch (sourceId)
                    {
                        case Session.PeerSourceId:
                            switch (type)
                            {
                                case Session.ConnectEvent:
                                    Session peer = (Session) source;
                                    Session session = new Session(this, this);
                                    m_sessions.Add(session);

                                    session.Accept(peer);
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
                default:
                    //  TODO: throw bad state
                    break;                                        
            }
        }        
    }
}
