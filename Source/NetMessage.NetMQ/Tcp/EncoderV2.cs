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
        private readonly PipeBase<NetMQMessage> m_pipeBase;
        private USocket m_usocket;
        private NetMQMessage m_message;

        enum State
        {
            Idle = 1, Sending, Done
        }

        private State m_state;
        private StateMachineEvent m_doneEvent;
        private bool m_signalPipe;
        private List<ArraySegment<byte>> m_bufferList;

        public EncoderV2(int sourceId, StateMachine owner, PipeBase<NetMQMessage> pipeBase)
            : base(sourceId, owner)
        {
            m_pipeBase = pipeBase;
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();
            m_bufferList = new List<ArraySegment<byte>>();
        }

        //public override bool IsIdle
        //{
        //    get
        //    {
        //        return IsStateMachineIdle;
        //    }
        //}

        public override void Start(USocket usocket, NetMQMessage message, bool signalPipe)
        {
            m_usocket = usocket;
            m_message = message;
            m_signalPipe = signalPipe;

            StartStateMachine();
        }              

        private void Send()
        {
            byte[] frameHeader;
            NetMQFrame frame;            

            m_bufferList.Clear();

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

                m_bufferList.Add(new ArraySegment<byte>(frameHeader));
                m_bufferList.Add(frame.Buffer);
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

            m_bufferList.Add(new ArraySegment<byte>(frameHeader));
            m_bufferList.Add(frame.Buffer);

            bool completedSync = m_usocket.Send(m_bufferList);

            if (completedSync)
            {
                Complete();
            }
            else
            {
                m_state = State.Sending;    
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

        private void Complete()
        {
            m_state = State.Done;
            m_message = null;

            if (m_signalPipe)
            {
                m_pipeBase.OnSent();
            }

            Raise(m_doneEvent, MessageSentEvent);
            StopStateMachine();            
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
                                    Complete();
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }


    }
}
