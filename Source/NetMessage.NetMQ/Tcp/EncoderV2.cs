using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using NetMessage.Core.Transport.Utils;

namespace NetMessage.NetMQ.Tcp
{
    public class EncoderV2 : EncoderBase
    {        
        public int BufferSize = 1024 * 8;

        enum State
        {
            Idle = 1, Active, Sending, Errored
        }
        
        private const int SendMessageAction = 2;

        private State m_state;
        private StateMachineEvent m_errorEvent;

        private PipeBase<NetMQMessage> m_pipeBase;
        private USocket m_usocket;

        private byte[] m_sendBuffer;
        private int m_position;
        private int m_bufferStartIndex;
       

        private NetMQMessage m_message;

        public EncoderV2(int sourceId, StateMachine owner, PipeBase<NetMQMessage> pipeBase)
            : base(sourceId, owner)
        {
            m_pipeBase = pipeBase;
            m_state = State.Idle;
            m_errorEvent = new StateMachineEvent();

            m_sendBuffer = new byte[BufferSize * 2];            

            m_position = 0;
            m_bufferStartIndex = 0;
        }

        public override void Dispose()
        {
            m_sendBuffer = null;
            m_errorEvent.Dispose();
            m_usocket = null;
            m_pipeBase = null;
            m_message = null;
            base.Dispose();
        }

        public override void Start(USocket usocket)
        {
            m_usocket = usocket;
           
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
                if ((position - m_bufferStartIndex) + size > BufferSize || size + position > m_sendBuffer.Length)
                {
                    // not enough space in buffer to send messages
                    return false;
                }

                m_sendBuffer[position] = (byte)(largeMessage | isLast);

                if (largeMessage == 2)
                {
                    BufferUtility.WriteLong(m_sendBuffer, position + 1, frame.MessageSize);
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

        protected override void Shutdown(int sourceId, int type, StateMachine source)
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
                                    m_state = State.Active;
                                    break;
                            }
                            break;
                    }
                    break;

                case State.Active:
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
                                        Raise(m_errorEvent, ErrorEvent);
                                        m_state = State.Errored;
                                        m_message = null;
                                    }
                                    else
                                    {
                                        // set the message to null if we were able to write the message to the buffer
                                        m_message = null;

                                        m_usocket.Send(m_sendBuffer, 0, m_position);
                                        m_bufferStartIndex = m_position;

                                        m_state = State.Sending;

                                        // if the message is written to the buffer we consider the message as sent
                                        m_pipeBase.OnSent();
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
                                case SendMessageAction:
                                    // we are currently sending, 

                                    // we add the message to the buffer, if the add succeed we consider the message as sent, signalling the pipe
                                    // and set the message to null, when the send will complete we will send the messages that were aggregated
                                    if (AddMessage(m_message))
                                    {
                                        m_message = null;
                                        m_pipeBase.OnSent();                                        
                                    }
                                    break;
                                case USocketSentAction:

                                    // if we have data that was not sent
                                    if (m_position != m_bufferStartIndex)
                                    {
                                        m_usocket.Send(m_sendBuffer, m_bufferStartIndex, m_position - m_bufferStartIndex);
                                        m_bufferStartIndex = m_position;                                        
                                    }
                                    // if we have message that was not able to be written because the buffer is full we send it now
                                    else if (m_message != null)
                                    {
                                        m_state = State.Active;
                                        Action(SendMessageAction);
                                    }
                                    else
                                    {
                                        // nothign to send, continue wait for a message
                                        m_state = State.Active;
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
