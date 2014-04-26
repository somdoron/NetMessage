using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Transport;
using NetMessage.Core.Transport.Utils;

namespace NetMessage.NetMQ.Tcp
{    
    public class DecoderV2 : DecoderBase
    {        
        enum State
        {
            Idle = 1,
            ReadingFlag,
            ReadingOneByteSize,
            ReadingEightByteSize,
            ReadingBody,
            HasMessage,
            Error,
        }        

        public const int BufferSize = 1024 * 8;        

        private const int NextAction = 2;
        private const int MessageReceivedAction = 3;

        private StateMachineEvent m_doneEvent;
        private State m_state;

        private byte[] m_buffer;
        private int m_bytesReceived;
        private int m_position;
        
        private bool m_isMore;
        private int m_size;

        private NetMQMessage m_message;
        private USocket m_usocket;

        private readonly PipeBase<NetMQMessage> m_pipeBase;

        public DecoderV2(int sourceId, StateMachine owner, PipeBase<NetMQMessage> pipeBase)
            : base(sourceId, owner)
        {
            m_pipeBase = pipeBase;
            m_buffer = new byte[BufferSize * 2];
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();
            m_bytesReceived = 0;
            m_position = 0;
        }    

        public override void Start(USocket usocket)
        {
            Debug.Assert(m_state == State.Idle);

            m_usocket = usocket;            

            StartStateMachine();
        }

        public override void Receive(out NetMQMessage message)
        {
            Debug.Assert(m_state == State.HasMessage);

            message = m_message;            
            m_message = new NetMQMessage();

            Action(MessageReceivedAction);
        }      

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == ActionSourceId && type == StopAction)
            {
                Debug.Assert(m_state == State.HasMessage || m_state == State.ReadingFlag);

                m_state = State.Idle;
                StoppedNoEvent();
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
                int flag = m_buffer[m_position];
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
                m_size = m_buffer[m_position];
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
                ReceiveMore();
            }
            else
            {
                long size = BufferUtility.ReadLong(m_buffer, m_position);

                m_position += 8;
                m_size = (int)size;

                m_state = State.ReadingBody;
                Action(NextAction);
            }
        }

        private void ReadBody()
        {
            if (m_size > BufferSize)
            {
                Debug.Assert(false, "Frame size too large");
                Raise(m_doneEvent, DecoderBase.ErrorEvent);
                m_state = State.Error;
            }
            else
            {
                int bytesLeft = m_bytesReceived - m_position;

                if (bytesLeft < m_size)
                {                    
                    ReceiveMore();
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
                        Buffer.BlockCopy(m_buffer, m_position, buffer, 0, m_size);
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
                        m_state = State.HasMessage;
                        m_pipeBase.OnReceived();                                               
                    }
                }
            }
        }
        
        private void ReceiveMore()
        {
            if (m_bytesReceived + BufferSize > m_buffer.Length)
            {
                m_bytesReceived = m_bytesReceived - m_position;

                // copy the data to the begining of the buffer
                Buffer.BlockCopy(m_buffer, m_position, m_buffer, 0, m_bytesReceived);

                m_position = 0;
            }

            m_usocket.Receive(m_buffer, m_bytesReceived, BufferSize);
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
                                    m_message = new NetMQMessage();
                                    m_state = State.ReadingFlag;
                                    Action(NextAction);
                                    break;
                            }
                            break;
                    }
                    break;
                case State.HasMessage:
                    switch (sourceId)
                    {
                        case ActionSourceId:
                            switch (type)
                            {
                                case MessageReceivedAction:
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
                                case USocketReceivedAction:
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
                                case USocketReceivedAction:
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
                                case USocketReceivedAction:
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
                                case USocketReceivedAction:
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

      
    }
}
