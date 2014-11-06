using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetMessage.AsyncIO;
using NetMessage.Transport;

namespace NetMessage.Core
{
    class Endpoint: StateMachine
    {
        public const int SourceId = 10;

        public const int EndpointStoppedEvent = 1;
        
        private const int StoppedAction = 1;

        enum State
        {
            Idle = 1,
            Active,
            Stopping,
        }

        

        private State m_state;
        private Socket m_socket;
        private EndpointOptions m_options;
        private string m_address;
        private EndpointBase m_endpointBase;

        private bool m_errored= false;

        public Endpoint(Socket socket, int endpointId, Transport.TransportBase transport, bool bind,
            string address)
            : base(SourceId, socket)
        {
            m_state = State.Idle;
            m_endpointBase = null;
            Id = endpointId;
            m_options = socket.EndpointTemplate.Clone();
            m_address = address;            
            m_socket = socket;

            if (bind)
            {
                m_endpointBase = transport.Bind(this);
            }
            else
            {
                m_endpointBase = transport.Connect(this);
            }
        }

        public int Id { get; private set; }

        public string Address
        {
            get
            {
                return m_address;
            }
        }

        public Socket Socket
        {
            get { return m_socket; }
        }

        public EndpointOptions Options
        {
            get { return m_options; }
        }

        public override void Dispose()
        {
            Debug.Assert(m_state == State.Idle);

            m_endpointBase.Dispose();
            base.Dispose();
        }

        public void Start()
        {
            StartStateMachine();
        }

        public void Stop()
        {
            StopStateMachine();
        }

        public void Stopped()
        {
            StateMachineStoppedEvent.StateMachine = this;
            StateMachineStoppedEvent.SourceId = StateMachine.ActionSourceId;
            StateMachineStoppedEvent.Source = null;
            StateMachineStoppedEvent.Type = StoppedAction;
            Context.Raise(StateMachineStoppedEvent);
        }

        public object GetOption( SocketOption option)
        {
            return m_socket.GetOptionInner(option);
        }

        public bool IsPeer(int socketType)
        {
            return m_socket.IsPeer(socketType);
        }

        internal override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == StateMachine.ActionSourceId && type == StateMachine.StopAction)
            {
                m_endpointBase.Stop();
                m_state = State.Stopping;
            }
            else if (m_state == State.Stopping)
            {
                if (sourceId == StateMachine.ActionSourceId && type == StoppedAction)
                {
                    m_state = State.Idle;
                    base.Stopped(EndpointStoppedEvent);
                }
            }
            else
            {
                // TODO: throw exception bad state
            }
        }

        internal override void Handle(int sourceId, int type, StateMachine source)
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
                            // TODO: throw bad source
                            break;
                    }
                    break;
                case State.Active:
                    // TODO: throw bad source
                    break;
                default:
                    // TODO: throw bad state
                    break;
            }
        }

        public void SetError()
        {
            if (!m_errored)
            {
                m_errored = true;
                // TODO: report the exception to the socket
            }
        }

        public void ClearError()
        {
            if (m_errored)
            {

                m_errored = false;
                // TODO: report the clear exception to the socket
            }
        } 

        
    }
}















