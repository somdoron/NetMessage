using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Core;
using NetMessage.Core.Transport;

namespace NetMessage.NetMQ.Tcp
{
    public class Session : StateMachine
    {
        public const int SourceId = 60;

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

        enum InState
        {
            Receiving,
            Stopping,
            HasMessage
        }

        enum OutState
        {
            Idle,
            Sending
        }

        private State m_state;

        private USocket m_usocket;
        private StateMachine m_usocketOwner;
        private int m_usocketOwnerSourceId;

        private Pipe m_pipe;

        private InState m_inState;
        private NetMQMessage m_inMessage;

        private OutState m_outState;
        private NetMQMessage m_outMesssage;



        private StateMachineEvent m_doneEvent;

        private DecodeV2 m_decoder;

        public Session(int sourceId, EndpointBase<NetMQMessage> endpoint, StateMachine owner)
            : base(sourceId, owner)
        {
            m_state = State.Idle;
            m_usocket = null;
            m_usocketOwner = null;
            m_usocketOwnerSourceId = -1;

            m_pipe = new Pipe(endpoint, this);

            m_doneEvent = new StateMachineEvent();
            m_decoder = new DecodeV2(DecoderSourceId, this);
        }

        public override void Dispose()
        {
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
            m_usocket.Stop();
        }

        private PipeStatus Send(NetMQMessage message)
        {
            byte[] frameHeader;
            NetMQFrame frame;

            List<ArraySegment<byte>> bufferList = new List<ArraySegment<byte>>(message.FrameCount * 2);

            for (int i = 0; i < message.FrameCount - 1; i++)
            {
                frame = message[i];

                if (frame.MessageSize > 255)
                {
                    frameHeader = new byte[9];
                    frameHeader[0] = 3;

                    Buffer.BlockCopy(BitConverter.GetBytes((long)IPAddress.HostToNetworkOrder(frame.MessageSize)),
                        0, frameHeader, 1, 8);
                }
                else
                {
                    frameHeader = new byte[2];
                    frameHeader[0] = 1;
                    frameHeader[1] = (byte)frame.MessageSize;
                }

                bufferList.Add(new ArraySegment<byte>(frameHeader));
                bufferList.Add(frame.Buffer);
            }

            frame = message.Last;

            if (frame.MessageSize > 255)
            {
                frameHeader = new byte[9];
                frameHeader[0] = 2;

                Buffer.BlockCopy(BitConverter.GetBytes((long)IPAddress.HostToNetworkOrder(frame.MessageSize)),
                    0, frameHeader, 1, 8);
            }
            else
            {
                frameHeader = new byte[2];
                frameHeader[0] = 0;
                frameHeader[1] = (byte)frame.MessageSize;
            }

            bufferList.Add(new ArraySegment<byte>(frameHeader));
            bufferList.Add(frame.Buffer);

            m_usocket.Send(bufferList);

            m_outState = OutState.Sending;

            return PipeStatus.Ok;
        }

        private PipeStatus Receive(out NetMQMessage message)
        {
            Debug.Assert(m_state == State.Active);
            Debug.Assert(m_inState == InState.HasMessage);            

            message = m_inMessage;
            m_inMessage = null;

            m_decoder.Start(m_usocket);
            m_inState = InState.Receiving;

            return PipeStatus.Ok;
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == ActionSourceId && type == StopAction)
            {
                m_pipe.Stop();

                if (!m_decoder.IsIdle)
                {
                    m_decoder.Stop();
                }

                m_state = State.Stopping;
            }

            if (m_state == State.Stopping)
            {
                if (m_decoder.IsIdle)
                {
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
                                    // TODO: this should be after the handshake
                                    m_pipe.Start();
                                    m_decoder.Start(m_usocket);
                                    m_inState = InState.Receiving;

                                    m_outState = OutState.Idle;
                                    m_state = State.Active;

                                    break;
                            }
                            break;
                    }
                    break;
                case State.Handshake:
                    break;
                case State.StoppingHandshake:
                    break;
                case State.Active:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ReceivedEvent:
                                    m_decoder.OnUSocketReceived();
                                    break;
                                case USocket.SentEvent:
                                    m_outState = OutState.Idle;
                                    m_pipe.OnSent();
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
                        case DecoderSourceId:
                            switch (type)
                            {
                                case DecodeV2.MessageReadyEvent:
                                    Debug.Assert(m_inState == InState.Receiving);

                                    m_inMessage = m_decoder.Message;
                                    m_decoder.Stop();
                                    m_inState = InState.Stopping;
                                    break;
                                case DecodeV2.StoppedEvent:
                                    Debug.Assert(m_inState == InState.Stopping);

                                    m_inState = InState.HasMessage;
                                    m_pipe.OnReceived();
                                    break;
                                case DecodeV2.ErrorEvent:
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
