using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Transport;

namespace NetMessage.NetMQ.Tcp
{
    public class ZMTPHandshake : HandshakeBase
    {
        enum State
        {
            Idle = 1,
            Sending,
            Receiving,
            SendingIdentity,
            ReceivingIdentity,
            StoppingDecoder,
            StoppingTimerError,
            StoppingTimerDone,
            Done,
            Stopping
        }

        private State m_state;
        private StateMachineEvent m_doneEvent;

        private const int USocketSourceId = 1;
        private const int TimerSourceId = 2;
        private const int DecoderSourceId = 3;
        private const int EncoderSourceId = 4;

        private const int Revision = 0x01;
        private const int SocketTypeByteLocation = 11;
        private const int RevisionLocation = 10;
        private const int GreetingSize = 12;

        private USocket m_usocket;
        private StateMachine m_usocketOwner;
        private int m_usocketOwnerId;

        private PipeBase<NetMQMessage> m_pipeBase;

        private byte[] m_outGreeting;
        private byte[] m_inGreeting;

        private int m_inGreetingReceived;

        private Timer m_timer;
        private NetMQMessage m_receivedIdentity;

        private DecoderBase m_decoder;
        private EncoderBase m_encoder;

        public ZMTPHandshake(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
            // This handshake implementation doesn't support identities
            m_outGreeting = new byte[GreetingSize]
            {
                0xff, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x01, 0x7f, Revision, 0x00          
            };

            m_inGreeting = new byte[GreetingSize];

            m_doneEvent = new StateMachineEvent();
            m_state = State.Idle;
            m_timer = new Timer(TimerSourceId, this);
        }

        public override void Dispose()
        {
            m_doneEvent.Dispose();
            m_timer.Dispose();
            base.Dispose();
        }

        public override bool IsIdle
        {
            get { return IsStateMachineIdle; }
        }

        public override DecoderBase Decoder
        {
            get
            {
                return m_decoder;
            }

        }

        public override EncoderBase Encoder
        {
            get
            {
                return m_encoder;
            }

        }

        public override void Start(USocket socket, PipeBase<NetMQMessage> pipe)
        {
            m_usocket = socket;
            m_pipeBase = pipe;

            m_usocketOwner = this;
            m_usocketOwnerId = USocketSourceId;
            m_usocket.SwapOwner(ref m_usocketOwner, ref m_usocketOwnerId);

            m_outGreeting[SocketTypeByteLocation] = (byte)(int)pipe.GetOption(SocketOption.Type);

            StartStateMachine();
        }

        public override void Stop()
        {
            StopStateMachine();
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == ActionSourceId && type == StopAction)
            {
                m_timer.Stop();
                m_state = State.Stopping;
            }

            if (m_state == State.Stopping)
            {
                if (!m_timer.IsIdle)
                    return;

                m_state = State.Idle;
                Stopped(StoppedEvent);
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
                                    // TODO: this should be an option 
                                    m_timer.Start(20000);
                                    bool completedSync = m_usocket.Send(m_outGreeting, 0, m_outGreeting.Length);

                                    if (completedSync)
                                    {
                                        m_inGreetingReceived = 0;
                                        m_usocket.Receive(m_inGreeting, 0, GreetingSize);
                                        m_state = State.Receiving;
                                    }
                                    else
                                    {
                                        m_state = State.Sending;    
                                    }
                                    
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Sending:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.SentEvent:
                                    m_inGreetingReceived = 0;
                                    m_usocket.Receive(m_inGreeting, 0, GreetingSize);
                                    m_state = State.Receiving;
                                    break;
                                case USocket.ShutdownEvent:
                                    break;
                                case USocket.ErrorEvent:
                                    m_timer.Stop();
                                    m_state = State.StoppingTimerError;
                                    break;
                            }
                            break;
                        case TimerSourceId:
                            switch (type)
                            {
                                case Timer.TimeOutEvent:
                                    m_timer.Stop();
                                    m_state = State.StoppingTimerError;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Receiving:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ReceivedEvent:
                                    m_inGreetingReceived += m_usocket.BytesReceived;

                                    if ((m_inGreetingReceived >= 1 && m_inGreeting[0] != m_outGreeting[0]) ||
                                        (m_inGreetingReceived >= 10 && m_inGreeting[9] != m_outGreeting[9]))
                                    {
                                        m_timer.Stop();
                                        m_state = State.StoppingTimerError;
                                        break;
                                    }

                                    if (m_inGreetingReceived < GreetingSize)
                                    {
                                        // we continue to wait for the greeting
                                        m_usocket.Receive(m_inGreeting, m_inGreetingReceived, GreetingSize - m_inGreetingReceived);
                                    }
                                    else
                                    {
                                        int revision = m_inGreeting[RevisionLocation];

                                        // any revision higher than one is supported
                                        if (revision < Revision)
                                        {
                                            m_timer.Stop();
                                            m_state = State.StoppingTimerError;
                                            break;
                                        }

                                        int socketType = m_inGreeting[SocketTypeByteLocation];

                                        // check if socket type okay
                                        if (!m_pipeBase.IsPeer(socketType))
                                        {
                                            m_timer.Stop();
                                            m_state = State.StoppingTimerError;
                                            break;
                                        }
                                        
                                        // sending the identity message, currently always sending empty message
                                        byte[] identity = new byte[2];

                                        if (m_usocket.Send(identity, 0, 2))
                                        {
                                            m_state = State.SendingIdentity;    
                                        }
                                        else
                                        {
                                            m_encoder = new EncoderV2(EncoderSourceId, this, m_pipeBase);
                                            Encoder.Start(m_usocket);

                                            m_decoder = new DecoderV2(DecoderSourceId, this);
                                            m_receivedIdentity = new NetMQMessage();
                                            Decoder.Start(m_usocket, m_receivedIdentity);
                                            m_state = State.ReceivingIdentity;  
                                        }
                                        
                                    }
                                    break;
                                case USocket.ShutdownEvent:
                                    break;
                                case USocket.ErrorEvent:
                                    m_timer.Stop();
                                    m_state = State.StoppingTimerError;
                                    break;
                            }
                            break;
                        case TimerSourceId:
                            switch (type)
                            {
                                case Timer.TimeOutEvent:
                                    m_timer.Stop();
                                    m_state = State.StoppingTimerError;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.SendingIdentity:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.SentEvent:
                                    // creating the encoder
                                    m_encoder = new EncoderV2(EncoderSourceId, this, m_pipeBase);
                                    Encoder.Start(m_usocket); 
                                    
                                    m_decoder = new DecoderV2(DecoderSourceId, this);
                                    m_receivedIdentity = new NetMQMessage();
                                    Decoder.Start(m_usocket, m_receivedIdentity);
                                    m_state = State.ReceivingIdentity;  
                                    break;
                                case USocket.ShutdownEvent:
                                    break;
                                case USocket.ErrorEvent:                                    
                                    m_timer.Stop();                                    
                                    m_state = State.StoppingTimerError;
                                    break;
                            }
                            break;                        
                        case TimerSourceId:
                            switch (type)
                            {
                                case Timer.TimeOutEvent:
                                    m_timer.Stop();                                    
                                    m_state = State.StoppingTimerError;
                                    break;
                            }
                            break;
                    }
                    break;                

                case State.ReceivingIdentity:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ReceivedEvent:
                                    Decoder.Received();
                                    break;
                                case USocket.ShutdownEvent:
                                    break;
                                case USocket.ErrorEvent:
                                    // TODO: stop decoder as well
                                    m_timer.Stop();
                                    m_decoder.Stop();
                                    m_state = State.StoppingTimerError;
                                    break;
                            }
                            break;
                        case DecoderSourceId:
                            switch (type)
                            {
                                case DecoderBase.MessageReceivedEvent:
                                    // we ignore the identity for the moment
                                    Debug.Assert(m_receivedIdentity.FrameCount == 1 && m_receivedIdentity.First.MessageSize <= 255);
                                    m_receivedIdentity = null;
                                    Decoder.Stop();
                                    m_state = State.StoppingDecoder;
                                    break;
                                case DecoderBase.ErrorEvent:
                                    m_timer.Stop();
                                    m_decoder.Stop();
                                    m_state = State.StoppingTimerError;
                                    break;
                                    break;
                            }
                            break;
                        case TimerSourceId:
                            switch (type)
                            {
                                case Timer.TimeOutEvent:
                                    m_timer.Stop();
                                    m_decoder.Stop();
                                    m_state = State.StoppingTimerError;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.StoppingDecoder:
                    switch (sourceId)
                    {
                        case DecoderSourceId:
                            switch (type)
                            {
                                case DecoderBase.StoppedEvent:
                                    // stopping the timer
                                    m_timer.Stop();
                                    m_state = State.StoppingTimerDone;
                                    break;
                            }
                            break;

                        case TimerSourceId:
                            switch (type)
                            {
                                case Timer.TimeOutEvent:
                                    m_timer.Stop();
                                    m_state = State.StoppingTimerError;
                                    break;
                            }
                            break;
                    }
                    break;

                case State.StoppingTimerError:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            // safe to ignore
                            break;
                        case TimerSourceId:
                            switch (type)
                            {
                                case Timer.StoppedEvent:
                                    if ((m_decoder == null || m_decoder.IsIdle))
                                    {
                                        DoneError();
                                    }
                                    break;
                            }
                            break;
                        case DecoderSourceId:
                            switch (type)
                            {
                                case DecoderBase.StoppedEvent:
                                    if (m_timer.IsIdle)
                                    {
                                        DoneError();
                                    }
                                    break;
                            }
                            break;                       
                    }
                    break;
                case State.StoppingTimerDone:
                    switch (sourceId)
                    {
                        case TimerSourceId:
                            switch (type)
                            {
                                case Timer.StoppedEvent:
                                    m_usocket.SwapOwner(ref m_usocketOwner, ref m_usocketOwnerId);
                                    m_usocket = null;
                                    m_usocketOwner = null;
                                    m_usocketOwnerId = -1;
                                    m_state = State.Done;
                                    Raise(m_doneEvent, DoneEvent);
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }

        private void DoneError()
        {
            m_usocket.SwapOwner(ref m_usocketOwner, ref m_usocketOwnerId);
            m_usocket = null;
            m_usocketOwner = null;
            m_usocketOwnerId = -1;
            m_state = State.Done;
            Raise(m_doneEvent, ErrorEvent);
        }
    }
}
