using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;

namespace NetMessage.NetMQ.Tcp
{
    public class EncoderV2 : EncoderBase
    {
        private USocket m_usocket;
        private NetMQMessage m_message;

        enum State
        {
            Idle = 1, Sending, Done
        }

        private State m_state;
        private StateMachineEvent m_doneEvent;

        public EncoderV2(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();
        }

        public override bool IsIdle
        {
            get
            {
                return IsStateMachineIdle;
            }
        }

        public override void Start(Core.AsyncIO.USocket usocket, NetMQMessage message)
        {
            m_usocket = usocket;
            m_message = message;

            StartStateMachine();
        }

        public override void Stop()
        {
            StopStateMachine();
        }

        protected override void Shutdown(int sourceId, int type, Core.AsyncIO.StateMachine source)
        {
            if (sourceId == ActionSourceId && type == StopAction)
            {
                m_state = State.Idle;
                Stopped(StoppedEvent);
            }
        }

        private void Send()
        {
            byte[] frameHeader;
            NetMQFrame frame;

            List<ArraySegment<byte>> bufferList = new List<ArraySegment<byte>>(m_message.FrameCount * 2);

            for (int i = 0; i < m_message.FrameCount - 1; i++)
            {
                frame = m_message[i];

                if (frame.MessageSize > 255)
                {
                    frameHeader = new byte[9];
                    frameHeader[0] = 3;

                    PutLong(frameHeader, 1, frame.MessageSize);
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

            frame = m_message.Last;

            if (frame.MessageSize > 255)
            {
                frameHeader = new byte[9];
                frameHeader[0] = 2;

                PutLong(frameHeader, 1, frame.MessageSize);
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

            m_state = State.Sending;
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
                                    Send();
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
                                    m_state = State.Done;
                                    m_message = null;
                                    Raise(m_doneEvent, MessageSentEvent);
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }


    }
}
