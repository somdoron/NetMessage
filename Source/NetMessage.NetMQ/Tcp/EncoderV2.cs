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

        private const int MinimumMessageSize = 1000;
        private const int SmallMessagesTimerInterval = 10;

        private const int SendMessageAction = 2;

        private State m_state;
        private StateMachineEvent m_doneEvent;

        private readonly PipeBase<NetMQMessage> m_pipeBase;
        private USocket m_usocket;
        
        private List<ArraySegment<byte>> m_bufferList;        
        private int m_totalPendingSize = 0;

        private Timer m_timer;

        public EncoderV2(int sourceId, StateMachine owner, PipeBase<NetMQMessage> pipeBase)
            : base(sourceId, owner)
        {
            m_pipeBase = pipeBase;
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();
            m_bufferList = new List<ArraySegment<byte>>();
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
            AddMessage(message);

            Action(SendMessageAction);
        }

        private void AddMessage(NetMQMessage message)
        {
            byte[] frameHeader;
            NetMQFrame frame;

            for (int i = 0; i < message.FrameCount; i++)
            {
                frame = message[i];

                int isLast = i == message.FrameCount - 1 ? 0 : 1;

                if (frame.MessageSize > 255)
                {
                    frameHeader = new byte[9];
                    frameHeader[0] = (byte)(2 | isLast);

                    PutLong(frameHeader, 1, frame.MessageSize);
                }
                else
                {
                    frameHeader = new byte[2];
                    frameHeader[0] = (byte)isLast;
                    frameHeader[1] = (byte)frame.MessageSize;
                }

                m_bufferList.Add(new ArraySegment<byte>(frameHeader));
                m_bufferList.Add(frame.Buffer);

                m_totalPendingSize += frame.MessageSize;
            }
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
                                    if (m_totalPendingSize > MinimumMessageSize || NoDelay)
                                    {
                                        bool completedSync = m_usocket.Send(m_bufferList);

                                        if (completedSync)
                                        {
                                            m_totalPendingSize = 0;
                                            m_bufferList.Clear();

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
                                    if (m_totalPendingSize > MinimumMessageSize || NoDelay)
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

                                    bool completedSync = m_usocket.Send(m_bufferList);
                                    if (completedSync)
                                    {
                                        m_totalPendingSize = 0;
                                        m_bufferList.Clear();

                                        if (SignalPipe)
                                        {
                                            m_pipeBase.OnSent();
                                        }

                                        Raise(m_doneEvent, MessageSentEvent);

                                        m_state = State.NoMessages;
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
                                    m_totalPendingSize = 0;
                                    m_bufferList.Clear();

                                    if (SignalPipe)
                                    {
                                        m_pipeBase.OnSent();
                                    }

                                    Raise(m_doneEvent, MessageSentEvent);

                                    m_state = State.NoMessages;
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }


    }
}
