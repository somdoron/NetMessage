using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Core;
using NetMessage.Core.Transport;

namespace NetMessage.NetMQ.Tcp
{
    public class Session : StateMachine
    {        
        public const int StoppedEvent = 1;
        public const int ErrorEvent = 2;

        class Pipe : PipeBase<NetMQMessage>
        {
            private readonly Session m_session;

            public Pipe(EndpointBase<NetMQMessage> endpointBase, Session session)
                : base(endpointBase)
            {
                m_session = session;
            }

            protected override Core.Core.PipeStatus SendInner(NetMQMessage message)
            {
                return m_session.Send(message);
            }

            protected override Core.Core.PipeStatus ReceiveInner(out NetMQMessage message)
            {
                return m_session.Receive(out message);
            }
        }

        private const int USocketSourceId = 1;
        private const int DecoderSourceId = 2;
        private const int EncoderSourceId = 3;
        private const int HandshakeSourceId = 4;

        enum State
        {
            Idle,
            Handshake,
            StoppingHandshake,
            Active,
            ShuttingDown,
            Done,
            Stopping
        }

        private State m_state;

        private USocket m_usocket;
        private StateMachine m_usocketOwner;
        private int m_usocketOwnerSourceId;

        private Pipe m_pipe;               

        private StateMachineEvent m_doneEvent;

        private HandshakeBase m_handshake;
        private DecoderBase m_decoder;
        private EncoderBase m_encoder;

        public Session(int sourceId, EndpointBase<NetMQMessage> endpoint, StateMachine owner)
            : base(sourceId, owner)
        {
            m_state = State.Idle;
            m_usocket = null;
            m_usocketOwner = null;
            m_usocketOwnerSourceId = -1;

            m_pipe = new Pipe(endpoint, this);

            m_doneEvent = new StateMachineEvent();
            m_handshake = new ZMTPHandshake(HandshakeSourceId, this);
        }

        public override void Dispose()
        {
            if (m_decoder != null)
            {
                m_decoder.Dispose();
            }

            if (m_encoder != null)
            {
                m_encoder.Dispose();
            }

            m_handshake.Dispose();

            m_doneEvent.Dispose();
            m_pipe.Dispose();
            base.Dispose();
        }

        public bool IsIdle
        {
            get
            {
                return IsStateMachineIdle;
            }
        }

        public void Start(USocket usocket)
        {
            m_usocket = usocket;

            m_usocketOwner = this;
            m_usocketOwnerSourceId = USocketSourceId;

            m_usocket.SwapOwner(ref m_usocketOwner, ref m_usocketOwnerSourceId);

            StartStateMachine();
        }

        public void Stop()
        {
            StopStateMachine();
        }

        private PipeStatus Send(NetMQMessage message)
        {
            Debug.Assert(m_state == State.Active);            

            m_encoder.Send(message);
            
            return PipeStatus.Ok;
        }

        private PipeStatus Receive(out NetMQMessage message)
        {
            Debug.Assert(m_state == State.Active);            
            
            m_decoder.Receive(out message);            

            return PipeStatus.Ok;
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == ActionSourceId && type == StopAction)
            {
                m_pipe.Stop();

                if (m_decoder != null && !m_decoder.IsIdle)
                {
                    m_decoder.Stop();
                }

                if (m_encoder != null && !m_encoder.IsIdle)
                {
                    m_encoder.Stop();
                }

                if (!m_handshake.IsIdle)
                {
                    m_handshake.Stop();
                }

                m_state = State.Stopping;
            }

            if (m_state == State.Stopping)
            {
                if ((m_decoder == null || m_decoder.IsIdle) && (m_encoder == null || m_encoder.IsIdle)  && m_handshake.IsIdle)
                {
                    m_usocket.SwapOwner(ref m_usocketOwner, ref m_usocketOwnerSourceId);
                    m_usocket = null;
                    m_usocketOwner = null;
                    m_usocketOwnerSourceId = -1;
                    m_state = State.Idle;
                    Stopped(StoppedEvent);
                }
            }
            else
            {
                // TODO: throw bad state
            }
        }


        protected override void Handle(int sourceId, int type, StateMachine source)
        {
            switch (m_state)
            {
                case State.Idle:
                    switch (sourceId)
                    {
                        case ActionSourceId:
                            switch (type)
                            {
                                case StartAction:
                                    m_handshake.Start(m_usocket, m_pipe);
                                    m_state = State.Handshake;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Handshake:
                    switch (sourceId)
                    {
                        case HandshakeSourceId:
                            switch (type)
                            {
                                case HandshakeBase.DoneEvent:                                    
                                    m_handshake.Stop();
                                    m_state = State.StoppingHandshake;
                                    break;
                                case HandshakeBase.ErrorEvent:
                                    m_state = State.Done;
                                    Raise(m_doneEvent, ErrorEvent);
                                    break;
                            }
                            break;
                    }
                    break;
                case State.StoppingHandshake:
                    switch (sourceId)
                    {
                        case HandshakeSourceId:
                            switch (type)
                            {
                                case HandshakeBase.StoppedEvent:
                                    m_decoder = m_handshake.CreateDecoder(DecoderSourceId, this);                                    
                                    m_encoder = m_handshake.CreateEncoder(EncoderSourceId, this);
                                    m_encoder.Start(m_usocket);
                                    m_decoder.Start(m_usocket);

                                    m_pipe.Start();
                                    
                                    m_state = State.Active;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Active:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ReceivedEvent:
                                    m_decoder.Received();
                                    break;
                                case USocket.SentEvent:
                                    m_encoder.Sent();
                                    break;
                                case USocket.ShutdownEvent:
                                    m_pipe.Stop();
                                    m_state = State.ShuttingDown;
                                    break;
                                case USocket.ErrorEvent:
                                    m_pipe.Stop();
                                    Raise(m_doneEvent, ErrorEvent);
                                    m_state = State.Done;
                                    break;
                            }
                            break;
                        case EncoderSourceId:
                            switch (type)
                            {                                
                                case EncoderBase.ErrorEvent:
                                    m_pipe.Stop();
                                    Raise(m_doneEvent, ErrorEvent);
                                    m_state = State.Done;
                                    break;
                            }
                            break;
                        case DecoderSourceId:
                            switch (type)
                            {                                
                                case DecoderBase.ErrorEvent:
                                    m_pipe.Stop();
                                    Raise(m_doneEvent, ErrorEvent);
                                    m_state = State.Done;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.ShuttingDown:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ErrorEvent:
                                    m_state = State.Done;
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
