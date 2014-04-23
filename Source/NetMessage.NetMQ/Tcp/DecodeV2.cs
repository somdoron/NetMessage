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
    public class DecodeV2 : StateMachine
    {
        public const int MessageReadyEvent = 1;        
        public const int StoppedEvent = 2;
        public const int ErrorEvent = 3;

        private const int USocketSourceId = 1;

        enum State
        {
            Idle = 1,
            ReadingFlag,
            ReadingOneByteSize,
            ReadingEightByteSize,
            ReadingBody,
            HasMessage,
            Errored
        }

        public const int ReceiveBufferSize = 1024 * 8;

        private StateMachineEvent m_doneEvent;
        private State m_state;

        private byte[] m_receiveBuffer;
        private bool m_isMore;
        private int m_size;

        private USocket m_usocket;


        public DecodeV2(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
            m_receiveBuffer = new byte[ReceiveBufferSize];
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();
        }

        public NetMQMessage Message
        {
            get;
            set;
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

            Debug.Assert(m_state == State.Idle);

            StartStateMachine();
        }

        public void Stop()
        {            
            StopStateMachine();
        }

        public void OnUSocketReceived()
        {
            Feed(USocketSourceId, USocket.ReceivedEvent, null);
        }

        public void OnUSocketError()
        {
            Feed(USocketSourceId, USocket.ErrorEvent, null);
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == ActionSourceId && type == StopAction)
            {
                Debug.Assert(m_state == State.HasMessage || m_state == State.Errored);
                Message = null;

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
                                    Message = new NetMQMessage();
                                    m_usocket.Receive(m_receiveBuffer, 0, 1);
                                    m_state = State.ReadingFlag;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.ReadingFlag:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ReceivedEvent:
                                    int flag = m_receiveBuffer[0];

                                    m_isMore = (flag & 1) == 1;

                                    if ((flag & 2) == 2)
                                    {
                                        m_usocket.Receive(m_receiveBuffer, 0, 8);
                                        m_state = State.ReadingEightByteSize;
                                    }
                                    else
                                    {
                                        m_usocket.Receive(m_receiveBuffer, 0, 1);
                                        m_state = State.ReadingOneByteSize;
                                    }

                                    break;                               
                            }
                            break;
                    }
                    break;

                case State.ReadingOneByteSize:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ReceivedEvent:
                                    m_size = m_receiveBuffer[0];

                                    if (m_size > 0)
                                    {
                                        m_usocket.Receive(m_receiveBuffer, 0, m_size);
                                        m_state = State.ReadingBody;    
                                    }
                                    else
                                    {
                                        Message.AppendEmptyFrame();

                                        if (m_isMore)
                                        {
                                            m_usocket.Receive(m_receiveBuffer, 0, 1);
                                            m_state = State.ReadingFlag;
                                        }
                                        else
                                        {
                                            Raise(m_doneEvent, MessageReadyEvent);
                                            m_state = State.HasMessage;
                                        }
                                    }
                                                                        
                                    break;                              
                            }
                            break;
                    }
                    break;

                case State.ReadingEightByteSize:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ReceivedEvent:

                                    long size = ReadLong(m_receiveBuffer, 0);

                                    if (size > ReceiveBufferSize)
                                    {
                                        // TODO: support large messages
                                        m_state = State.Errored;
                                        Raise(m_doneEvent, ErrorEvent);
                                    }
                                    m_size = (int)size;

                                    m_usocket.Receive(m_receiveBuffer, 0, m_size);
                                    m_state = State.ReadingBody;

                                    break;                                
                            }
                            break;
                    }
                    break;
                case State.ReadingBody:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ReceivedEvent:

                                    byte[] buffer = new byte[m_size];
                                    Buffer.BlockCopy(m_receiveBuffer, 0, buffer, 0, m_size);
                                    Message.Append(buffer);

                                    if (m_isMore)
                                    {
                                        m_usocket.Receive(m_receiveBuffer, 0, 1);
                                        m_state = State.ReadingFlag;
                                    }
                                    else
                                    {
                                        Raise(m_doneEvent, MessageReadyEvent);
                                        m_state = State.HasMessage;
                                    }

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
