using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Transport;

namespace NetMessage.NetMQ.Tcp
{
    public class EncoderV2 : EncoderBase
    {
        public int SocketSendBufferSize = 1024 * 2;

        enum State
        {
            Idle = 1, NoMessages, Sending, Errored
        }

        private const int TimerSourceId = 1;

        private const int MaximumPacketSize = 1460;
        private const int SmallMessagesTimerInterval = 10;

        private const int SendMessageAction = 2;

        private State m_state;
        private StateMachineEvent m_doneEvent;

        private readonly PipeBase<NetMQMessage> m_pipeBase;
        private USocket m_usocket;

        private Timer m_timer;

        private byte[] m_sendBuffer;
        private int m_position;
        private int m_bufferStartIndex;
        private int m_sendBufferSize;

        private NetMQMessage m_message;

        public EncoderV2(int sourceId, StateMachine owner, PipeBase<NetMQMessage> pipeBase)
            : base(sourceId, owner)
        {
            m_pipeBase = pipeBase;
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();

            m_sendBufferSize = (int)pipeBase.GetOption(SocketOption.SendBuffer) - SocketSendBufferSize;

            m_sendBuffer = new byte[m_sendBufferSize * 2];
            m_timer = new Timer(TimerSourceId, this);

            m_position = 0;
            m_bufferStartIndex = 0;
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

        public override void Start(USocket usocket)
        {
            m_usocket = usocket;
            m_usocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, SocketSendBufferSize);

            StartStateMachine();
        }

        public override void Send(NetMQMessage message)
        {
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

                int size = frame.MessageSize + (largeMessage == 2 ? 9 : 2);
                if ((position - m_bufferStartIndex) + size > m_sendBufferSize || size + position > m_sendBuffer.Length)
                {
                    // not enough space in buffer to send messages
                    return false;
                }

                m_sendBuffer[position] = (byte)(largeMessage | isLast);

                if (largeMessage == 2)
                {
                    PutLong(m_sendBuffer, position + 1, frame.MessageSize);
                    position += 9;
                }
                else
                {
                    m_sendBuffer[position + 1] = (byte)frame.MessageSize;
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
                                    m_position = 0;
                                    m_bufferStartIndex = 0;

                                    // we don't support messages that are larger than the buffer
                                    if (!AddMessage(m_message))
                                    {
                                        Raise(m_doneEvent, ErrorEvent);
                                        m_state = State.Errored;
                                        m_message = null;
                                        return;
                                    }

                                    m_message = null;

                                    m_usocket.Send(m_sendBuffer, 0, m_position);
                                    m_bufferStartIndex = m_position;

                                    m_state = State.Sending;

                                    // because the buffer is not full we let the session and pipe now the message was sent
                                    m_pipeBase.OnSent();
                                    Raise(m_doneEvent, MessageSentEvent);

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
                                case SendMessageAction:
                                    // we are currently sending, let's collect the messages

                                    // only if buffer is not full continue, if not, we are not signalling the pipe so no new messages will come
                                    if (AddMessage(m_message))
                                    {
                                        m_message = null;
                                        m_pipeBase.OnSent();

                                        Raise(m_doneEvent, MessageSentEvent);
                                    }
                                    break;
                                case MessageSentEvent:

                                    // if we have data waiting
                                    if (m_position != m_bufferStartIndex)
                                    {
                                        m_usocket.Send(m_sendBuffer, m_bufferStartIndex, m_position - m_bufferStartIndex);
                                        m_bufferStartIndex = m_position;                                        
                                    }
                                    else if (m_message != null)
                                    {
                                        m_state = State.NoMessages;
                                        Action(SendMessageAction);
                                    }
                                    else
                                    {
                                        m_state = State.NoMessages;
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
