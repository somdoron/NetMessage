using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Transport;

namespace NetMessage.NetMQ.Tcp
{
    public class EncoderV2 : EncoderBase
    {        
        enum State
        {
            Idle = 1, NoMessages, WaitingForMoreMessages, StoppingTimer, Sending
        }

        private const int TimerSourceId = 1;

        private int SendBufferSize = 1024*8;

        private const int MinimumMessageSize = 1024 * 2;
        private const int SmallMessagesTimerInterval = 50;

        private const int SendMessageAction = 2;

        private State m_state;
        private StateMachineEvent m_doneEvent;

        private readonly PipeBase<NetMQMessage> m_pipeBase;
        private USocket m_usocket;
                
        private Timer m_timer;

        private NetMQMessage m_message;        
        private byte[] m_sendBuffer;
        private int m_position;

        public EncoderV2(int sourceId, StateMachine owner, PipeBase<NetMQMessage> pipeBase)
            : base(sourceId, owner)
        {
            m_pipeBase = pipeBase;
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();
            m_sendBuffer = new byte[SendBufferSize];
            m_timer = new Timer(TimerSourceId, this);
        }

        //public override bool IsIdle
        //{
        //    get
        //    {
        //        return IsStateMachineIdle;
        //    }
        //}

        public override bool NoDelay
        {
            get;
            set;
        }

        public override bool SignalPipe { get; set; }

        public override void Start(USocket usocket)
        {
            m_usocket = usocket;

            StartStateMachine();
        }

        public override void Send(NetMQMessage message)
        {            
            //AddMessage(message);
            m_message = message;

            Action(SendMessageAction);
        }

        private bool AddMessage(NetMQMessage message)
        {
            int position = m_position;
            NetMQFrame frame;

            for (int i = 0; i < message.FrameCount; i++)
            {
                frame = message[i];                

                int largeMessage = frame.MessageSize > 255 ? 2 : 0;
                int isLast = i == message.FrameCount - 1 ? 0 : 1;

                int size = frame.MessageSize + largeMessage == 2 ? 9 : 2;
                if (size > SendBufferSize - position)
                {
                    // not enough space in buffer to send messages
                    return false;
                }

                m_sendBuffer[position] = (byte)(largeMessage | isLast);                                       

                if (largeMessage == 2)
                {                    
                    PutLong(m_sendBuffer, position+1, frame.MessageSize);
                    position += 9;
                }
                else
                {                    
                    m_sendBuffer[position+1] = (byte)frame.MessageSize;
                    position += 2;
                }

                Buffer.BlockCopy(frame.Buffer.Array, frame.Buffer.Offset, m_sendBuffer, position, frame.MessageSize);
                position += frame.MessageSize;
            }

            m_position = position;
            return true;
        }

        private void PutLong(byte[] buffer, int offset, long value)
        {
            buffer[offset] = (byte)(((value) >> 56) & 0xff);
            buffer[offset + 1] = (byte)(((value) >> 48) & 0xff);
            buffer[offset + 2] = (byte)(((value) >> 40) & 0xff);
            buffer[offset + 3] = (byte)(((value) >> 32) & 0xff);
            buffer[offset + 4] = (byte)(((value) >> 24) & 0xff);
            buffer[offset + 5] = (byte)(((value) >> 16) & 0xff);
            buffer[offset + 6] = (byte)(((value) >> 8) & 0xff);
            buffer[offset + 7] = (byte)(value & 0xff);
        }

        protected override void Shutdown(int sourceId, int type, Core.AsyncIO.StateMachine source)
        {
            if (sourceId == ActionSourceId && type == StopAction)
            {
                m_state = State.Idle;
                StoppedNoEvent();
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
                                    m_state = State.NoMessages;
                                    break;
                            }
                            break;
                    }
                    break;

                case State.NoMessages:
                    switch (sourceId)
                    {
                        case ActionSourceId:
                            switch (type)
                            {
                                case SendMessageAction:
                                    if (!AddMessage(m_message))
                                    {
                                        // TODO: handle very big message
                                    }

                                    m_message = null;

                                    if (m_position > MinimumMessageSize || NoDelay)
                                    {                                                                                
                                        bool completedSync = m_usocket.Send(m_sendBuffer, 0, m_position);

                                        if (completedSync)
                                        {
                                            m_position = 0;                                            

                                            if (SignalPipe)
                                            {
                                                m_pipeBase.OnSent();
                                            }

                                            Raise(m_doneEvent, MessageSentEvent);

                                            // No state change
                                        }
                                        else
                                        {
                                            m_state = State.Sending;
                                        }
                                    }
                                    else
                                    {
                                        if (SignalPipe)
                                        {
                                            m_pipeBase.OnSent();
                                        }

                                        Raise(m_doneEvent, MessageSentEvent);

                                        m_timer.Start(SmallMessagesTimerInterval);

                                        m_state = State.WaitingForMoreMessages;
                                    }

                                    break;
                            }
                            break;
                    }
                    break;
                case State.WaitingForMoreMessages:
                    switch (sourceId)
                    {
                        case ActionSourceId:
                            switch (type)
                            {
                                case SendMessageAction:
                                    if (!AddMessage(m_message))
                                    {
                                        // send the current buffer, we will send the next message later
                                        m_timer.Stop();
                                        m_state = State.StoppingTimer;
                                    }
                                    else
                                    {
                                        m_message = null;

                                        if (m_position > MinimumMessageSize || NoDelay)
                                        {
                                            m_timer.Stop();
                                            m_state = State.StoppingTimer;
                                        }
                                        else
                                        {
                                            if (SignalPipe)
                                            {
                                                m_pipeBase.OnSent();
                                            }

                                            Raise(m_doneEvent, MessageSentEvent);
                                        }
                                    }
                                    break;
                            }
                            break;
                        case TimerSourceId:
                            switch (type)
                            {
                                case Timer.TimeOutEvent:
                                    m_timer.Stop();
                                    m_state = State.StoppingTimer;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.StoppingTimer:
                    switch (sourceId)
                    {
                        case TimerSourceId:
                            switch (type)
                            {
                                case Timer.StoppedEvent:

                                    bool completedSync = m_usocket.Send(m_sendBuffer, 0, m_position);
                                    if (completedSync)
                                    {
                                        m_position = 0;                                        

                                        if (SignalPipe)
                                        {
                                            m_pipeBase.OnSent();
                                        }

                                        Raise(m_doneEvent, MessageSentEvent);

                                        m_state = State.NoMessages;

                                        // message is waiting to be sent
                                        if (m_message != null)
                                        {
                                            Action(SendMessageAction);
                                        }
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
                        case ActionSourceId:
                            switch (type)
                            {
                                case MessageSentEvent:
                                    m_position = 0;

                                    if (SignalPipe)
                                    {
                                        m_pipeBase.OnSent();
                                    }

                                    Raise(m_doneEvent, MessageSentEvent);

                                    m_state = State.NoMessages;

                                    // message is waiting to be sent
                                    if (m_message != null)
                                    {
                                        Action(SendMessageAction);
                                    }

                                    break;
                            }
                            break;
                    }
                    break;
            }
        }


    }
}
