using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;

namespace NetMessage.NetMQ.Tcp
{
    /// <summary>
    /// First implementation of ZMTP v2 decoder, not very fast (multiple receive calls for each frame)
    /// Doesn't support message size larger than 8192
    /// </summary>
    public class DecoderV2 : DecoderBase
    {
        enum State
        {
            Idle = 1,
            ReadingFlag,
            ReadingOneByteSize,
            ReadingEightByteSize,
            ReadingBody,
            Done,
        }

        public const int ReceiveBufferSize = 1024 * 8;

        public const int MinimumBytesToRead = 1024*2;

        private const int NextAction = 2;

        private StateMachineEvent m_doneEvent;
        private State m_state;

        private byte[] m_receiveBuffer;
        private int m_bytesReceived;
        private int m_position;
        
        private bool m_isMore;
        private int m_size;

        private NetMQMessage m_message;
        private USocket m_usocket;

        public DecoderV2(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
            m_receiveBuffer = new byte[ReceiveBufferSize];
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();
            m_bytesReceived = 0;
            m_position = 0;
        }

        public override bool IsIdle
        {
            get
            {
                return IsStateMachineIdle;
            }
        }

        public override void Start(USocket usocket, NetMQMessage message)
        {
            Debug.Assert(m_state == State.Idle);

            m_usocket = usocket;
            m_message = message;

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
                Debug.Assert(m_state == State.Done || m_state == State.ReadingFlag);

                m_state = State.Idle;
                Stopped(StoppedEvent);
            }
        }

        private void ReadFlag()
        {
            if (m_bytesReceived - m_position == 0)
            {
                ReceiveMore();
            }
            else
            {
                int flag = m_receiveBuffer[m_position];
                m_position++;

                m_isMore = (flag & 1) == 1;

                if ((flag & 2) == 2)
                {
                    m_state = State.ReadingEightByteSize;
                }
                else
                {
                    m_state = State.ReadingOneByteSize;
                }

                Action(NextAction);
            }
        }

        private void ReadOneByteSize()
        {
            if (m_bytesReceived - m_position == 0)
            {
                ReceiveMore();
            }
            else
            {
                m_size = m_receiveBuffer[m_position];
                m_position++;

                m_state = State.ReadingBody;
                Action(NextAction);
            }
        }

        private void ReadEightByteSize()
        {
            // check if we have 8 bytes to read
            if (m_bytesReceived - m_position < 8)
            {
                ReceiveMore(8 - (m_bytesReceived - m_position));
            }
            else
            {
                long size = ReadLong(m_receiveBuffer, m_position);

                m_position += 8;
                m_size = (int)size;

                m_state = State.ReadingBody;
                Action(NextAction);
            }
        }

        private void ReadBody()
        {
            if (m_size > ReceiveBufferSize)
            {
                Debug.Assert(false, "Frame size too large");
                Raise(m_doneEvent, DecoderBase.ErrorEvent);
                m_state = State.Done;
            }
            else
            {
                int bytesLeft = m_bytesReceived - m_position;

                if (bytesLeft < m_size)
                {                    
                    ReceiveMore(m_size - bytesLeft);
                }
                else
                {
                    if (m_size == 0)
                    {
                        m_message.AppendEmptyFrame();
                    }
                    else
                    {                        
                        byte[] buffer = new byte[m_size];
                        Buffer.BlockCopy(m_receiveBuffer, m_position, buffer, 0, m_size);
                        m_message.Append(buffer);

                        m_position += m_size;
                    }

                    if (m_isMore)
                    {
                        m_state = State.ReadingFlag;
                        Action(NextAction);
                    }
                    else
                    {
                        m_message = null;
                        Raise(m_doneEvent, DecoderBase.MessageReceivedEvent);
                        m_state = State.Done;
                    }
                }
            }
        }
        
        private void ReceiveMore(int bytesNeed = 1)
        {
            int minimumToReceive = bytesNeed;

            if (minimumToReceive < MinimumBytesToRead)
            {
                minimumToReceive = MinimumBytesToRead;
            }

            if (m_bytesReceived + minimumToReceive > ReceiveBufferSize)
            {
                m_bytesReceived = m_bytesReceived - m_position;

                // copy the data to the begining of the buffer
                Buffer.BlockCopy(m_receiveBuffer, m_position, m_receiveBuffer, 0, m_bytesReceived);

                m_position = 0;
            }

            m_usocket.Receive(m_receiveBuffer, m_bytesReceived, ReceiveBufferSize - m_bytesReceived);
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
                                    m_state = State.ReadingFlag;
                                    Action(NextAction);

                                    break;
                            }
                            break;
                    }
                    break;
                case State.ReadingFlag:
                    switch (sourceId)
                    {
                        case ActionSourceId:
                            switch (type)
                            {
                                case ReceivedAction:
                                    m_bytesReceived += m_usocket.BytesReceived;
                                    ReadFlag();
                                    break;
                                case NextAction:
                                    ReadFlag();
                                    break;
                            }
                            break;
                    }
                    break;

                case State.ReadingOneByteSize:
                    switch (sourceId)
                    {
                        case ActionSourceId:
                            switch (type)
                            {
                                case ReceivedAction:
                                    m_bytesReceived += m_usocket.BytesReceived;
                                    ReadOneByteSize();
                                    break;
                                case NextAction:
                                    ReadOneByteSize();
                                    break;
                            }
                            break;
                    }
                    break;

                case State.ReadingEightByteSize:
                    switch (sourceId)
                    {
                        case ActionSourceId:
                            switch (type)
                            {
                                case ReceivedAction:
                                    m_bytesReceived += m_usocket.BytesReceived;
                                    ReadEightByteSize();
                                    break;
                                case NextAction:
                                    ReadEightByteSize();
                                    break;
                            }
                            break;
                    }
                    break;
                case State.ReadingBody:
                    switch (sourceId)
                    {
                        case ActionSourceId:
                            switch (type)
                            {
                                case ReceivedAction:
                                    m_bytesReceived += m_usocket.BytesReceived;
                                    ReadBody();
                                    break;
                                case NextAction:
                                    ReadBody();
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }

        private long ReadLong(byte[] bytes, int offset)
        {
            return (((long)bytes[offset]) << 56) |
                    (((long)bytes[offset + 1]) << 48) |
                    (((long)bytes[offset + 2]) << 40) |
                    (((long)bytes[offset + 3]) << 32) |
                    (((long)bytes[offset + 4]) << 24) |
                    (((long)bytes[offset + 5]) << 16) |
                    (((long)bytes[offset + 6]) << 8) |
                    ((long)bytes[offset + 7]);
        }
    }
}
