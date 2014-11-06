using System;
using System.Collections.Generic;
using System.Diagnostics;
using NetMessage.Core;
using NetMessage.AsyncIO;
using NetMessage.Core;
using NetMessage.Transport;

namespace NetMessage.Transport.InProc
{
    class Session : StateMachine
    {
        class Pipe : PipeBase
        {
            private readonly Session m_session;

            public Pipe(EndpointBase endpointBase, Session session)
                : base(endpointBase)
            {
                m_session = session;
            }

            protected override PipeStatus SendInner(Message message)
            {
                return m_session.Send(message);
            }

            protected override PipeStatus ReceiveInner(out Message message)
            {
                return m_session.Receive(out message);
            }
        }

        public const int PeerSourceId = 27713;
        public const int SourceId = 10;

        public const int ConnectEvent = 1;
        public const int ReadyEvent = 2;
        public const int AcceptedEvent = 3;
        public const int SentEvent = 4;
        public const int ReceivedEvent = 5;
        public const int DisconnectEvent = 6;
        public const int StoppedEvent = 7;

        public const int ReadyAction = 1;
        public const int AcceptedAction = 2;

        enum State
        {
            Idle = 1,
            Connecting = 2,
            Ready = 3,
            Active = 4,
            Disconnected = 5,
            StoppingPeer = 6,
            Stopping = 7,
        }

        [Flags]
        enum SessionFlags
        {
            None = 0,
            Sending = 1,
            Receiving = 2
        }

        private State m_state;
        private SessionFlags m_flags;

        private Session m_peer;
        private Pipe m_pipe;

        private StateMachineEvent m_connectEvent;
        private StateMachineEvent m_sentEvent;
        private StateMachineEvent m_receivedEvent;
        private StateMachineEvent m_disconnectedEvent;

        private Queue<Message> m_messageQueue;

        // This message is the one being sent from this session to the peer
        // session. It holds the data only temporarily, until the peer moves
        // it to its msgqueue.
        private Message m_message;

        public Session(EndpointBase endpointBase, StateMachine owner) :
            base(SourceId, owner)
        {            
            m_state = State.Idle;
            m_flags = SessionFlags.None;
            m_peer = null;

            m_pipe = new Pipe(endpointBase, this);

            int receiveBuffer = (int)
                endpointBase.GetOption(SocketOption.ReceiveBuffer);

            // TODO: limit the maximum memory of the queue
            m_messageQueue = new Queue<Message>();
            m_message = null;

            m_connectEvent = new StateMachineEvent();
            m_sentEvent = new StateMachineEvent();
            m_receivedEvent = new StateMachineEvent();
            m_disconnectedEvent = new StateMachineEvent();
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
            m_connectEvent.Dispose();
            m_sentEvent.Dispose();
            m_receivedEvent.Dispose();
            m_disconnectedEvent.Dispose();
            m_pipe.Dispose();
            base.Dispose();
        }

        public void Connect(StateMachine peer)
        {
            StartStateMachine();

            base.RaiseTo(peer, m_connectEvent, PeerSourceId, ConnectEvent, this);
        }

        public void Accept(Session peer)
        {
            m_peer = peer;
            base.RaiseTo(peer, m_connectEvent, PeerSourceId, ReadyEvent, this);

            StartStateMachine();
            Action(ReadyAction);
        }

        public void Stop()
        {
            StopStateMachine();
        }


        private PipeStatus Send(Message message)
        {
            if (m_state == State.Disconnected)
                throw new NetMessageException(NetMessageErrorCode.ConnectionReset);

            Debug.Assert(m_state == State.Active);
            Debug.Assert(!m_flags.HasFlag(SessionFlags.Sending));

            m_message = message;

            m_flags |= SessionFlags.Sending;

            base.RaiseTo(m_peer, m_sentEvent, PeerSourceId, SentEvent, this);

            return PipeStatus.Ok;
        }

        private PipeStatus Receive(out Message message)
        {
            Debug.Assert(m_state == State.Active || m_state == State.Disconnected);

            message = m_messageQueue.Dequeue();

            if (m_state != State.Disconnected)
            {
                if (m_flags.HasFlag(SessionFlags.Receiving))
                {
                    m_messageQueue.Enqueue(m_peer.m_message);

                    //  TODO: check if the queue is full
                    m_peer.m_message = null;
                    base.RaiseTo(m_peer, m_receivedEvent, PeerSourceId, ReceivedEvent, this);
                    m_flags &= ~SessionFlags.Receiving;
                }
            }

            if (m_messageQueue.Count > 0)
            {
                m_pipe.OnReceived();
            }

            return PipeStatus.Ok;            
        }

        private void ShutdownEvents(int sourceId, int type)
        {
            bool handled = false;

            switch (sourceId)
            {
                case StateMachine.ActionSourceId:
                    switch (type)
                    {
                        case StopAction:
                            if (m_state != State.Idle && m_state != State.Disconnected)
                            {
                                m_pipe.Stop();                                                              
                                
                                base.RaiseTo(m_peer, m_disconnectedEvent, PeerSourceId, DisconnectEvent,this);

                                m_state = State.StoppingPeer;
                            }
                            else
                            {
                                m_state = State.Stopping;
                            }
                            handled = true;
                            break;
                    }
                    break;
                case PeerSourceId:
                    switch (type)
                    {
                        case ReceivedEvent:
                            handled = true;
                            break;
                    }
                    break;
            }

            if (!handled)
            {
                switch (m_state)
                {
                    case State.StoppingPeer:
                        switch (sourceId)
                        {
                            case PeerSourceId:
                                switch (type)
                                {
                                    case DisconnectEvent:
                                        m_state = State.Stopping;
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
                    default:
                    // TODO: throw bad state
                        break;
                }
            }
        }

        internal override void Shutdown(int sourceId, int type, StateMachine source)
        {
            ShutdownEvents(sourceId, type);

            if (m_state == State.Stopping && !m_receivedEvent.Active && !m_disconnectedEvent.Active)
            {
                Stopped(StoppedEvent);
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
                                case StartAction:
                                    m_state = State.Connecting;
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
                case State.Connecting:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case ReadyAction:
                                    m_state = State.Ready;
                                    break;
                                default:
                                    // TODO: throw bad action
                                    break;
                            }
                            break;
                        case PeerSourceId:
                            switch (type)
                            {
                                case ReadyEvent:
                                    m_peer = (Session)source;
                                    m_pipe.Start();

                                    m_state = State.Active;
                                    base.RaiseTo(m_peer, m_connectEvent, PeerSourceId, AcceptedEvent, this);
                                    break;
                                default:
                                // TODO:throw bad action
                                    break;
                            }
                            break;
                        default:
                        // TODO: throw bad source
                            break;
                    }
                    break;
                case State.Ready:
                    switch (sourceId)
                    {
                        case PeerSourceId:
                            switch (type)
                            {
                                case ReadyEvent:
                                    m_pipe.Start();
                                    m_state = State.Active;
                                    break;
                                case AcceptedEvent:
                                    m_pipe.Start();
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
                    switch (sourceId)
                    {
                        case PeerSourceId:
                            switch (type)
                            {
                                case SentEvent:
                                    bool empty = m_messageQueue.Count == 0;
                                    m_messageQueue.Enqueue(m_peer.m_message);
                                    // TODO: handle eagain, queue is full
                                    m_peer.m_message = null;

                                    if (empty)
                                    {
                                        m_pipe.OnReceived();
                                    }

                                    base.RaiseTo(m_peer, m_receivedEvent, PeerSourceId, ReceivedEvent, this);

                                    break;
                                case ReceivedEvent:
                                    m_pipe.OnSent();
                                    m_flags &= ~SessionFlags.Sending;
                                    break;
                                case DisconnectEvent:
                                    m_pipe.Stop();
                                    base.RaiseTo(m_peer, m_disconnectedEvent, PeerSourceId, DisconnectEvent, this);
                                    m_state = State.Disconnected;
                                    break;
                                default:
                                    // TODO:throw bad action
                                    break;
                            }
                            break;
                        default:
                        // TODO: throw bad source
                            break;
                    }
                    break;
                case State.Disconnected:
                    switch (sourceId)
                    {
                        case PeerSourceId:
                            switch (type)
                            {
                                case ReceivedEvent:
                                // This case can safely be ignored. It may happen when
                                // nn_close() comes before the already enqueued
                                // NN_SINPROC_RECEIVED has been delivered. 
                                    break;
                                default:
                                    // TODO:throw bad action
                                    break;
                            }
                            break;
                        default:
                            // TODO: throw bad source
                            break;
                    }
                    break;
                default:
                    // TODO: throw bad state
                    break;
            }
        }
    }
}
