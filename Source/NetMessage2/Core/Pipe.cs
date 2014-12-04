using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetMessage2.Core
{
    public class Pipe : Actor
    {
        public class ActivateRead
        {

        }

        public class ActivateWrite
        {
            public ActivateWrite(long numberOfMessagesRead)
            {
                NumberOfMessagesRead = numberOfMessagesRead;
            }

            public long NumberOfMessagesRead { get; private set; }
        }

        //  Maximal delta between high and low watermark.
        public const int MaxWatermarkDelta = (1024);

        //  Underlying pipes for both directions.
        private Utils.Queue<Message> m_inboundQueue;
        private Utils.Queue<Message> m_outboundQueue;

        //  Can the pipe be read from / written to?
        private bool m_inActive;
        private bool m_outActive;

        //  High watermark for the outbound pipe.
        private readonly int m_highWatermark;

        //  Low watermark for the inbound pipe.
        private readonly int m_lowWatermark;

        //  Number of messages read and written so far.
        private long m_numberOfMessagesRead;
        private long m_numberOfMessagesWritten;

        //  Last received peer's msgs_read. The actual number in the peer
        //  can be higher at the moment.
        private long m_peersMsgsRead;

        //  The pipe object on the other side of the pipepair.
        private ActorAddress m_peer;

        private ActorAddress m_parent;

        /// <summary> Specifies the state of the pipe endpoint. </summary>
        enum State
        {
            /// <summary> Active is common state before any termination begins. </summary>
            Active,
            /// <summary> Delimited means that delimiter was read from pipe before term command was received. </summary>
            Delimited,
            /// <summary> Pending means that term command was already received from the peer but there are still pending messages to read. </summary>
            Pending,
            /// <summary> Terminating means that all pending messages were already read and all we are waiting for is ack from the peer. </summary>
            Terminating,
            /// <summary> Terminated means that 'terminate' was explicitly called by the user. </summary>
            Terminated,
            /// <summary> Double_terminated means that user called 'terminate' and then we've got term command from the peer as well. </summary>
            DoubleTerminated
        } ;
        private State m_state;

        public Pipe(ActorAddress parent, IMailbox mailbox, Utils.Queue<Message> inboundQueue,
            Utils.Queue<Message> outboundQueue, int inHighWatermark, int outHighWatermark) :
            base(parent, mailbox)
        {
            m_parent = parent;
            m_inboundQueue = inboundQueue;
            m_outboundQueue = outboundQueue;
            m_inActive = true;
            m_outActive = true;
            m_highWatermark = outHighWatermark;
            m_lowWatermark = (inHighWatermark > MaxWatermarkDelta * 2) ? inHighWatermark - MaxWatermarkDelta : (inHighWatermark + 1) / 2;
            m_numberOfMessagesRead = 0;
            m_numberOfMessagesWritten = 0;
            m_peersMsgsRead = 0;
            m_peer = null;
            m_state = State.Active;

            Receive<ActivateRead>(OnAcitvateRead);
            Receive<ActivateWrite>(OnAcitvateWrite);
        }

        public bool CanRead
        {
            get
            {
                if (!m_inActive || (m_state != State.Active && m_state != State.Pending))
                    return false;

                //  Check if there's an item in the pipe.
                if (!m_inboundQueue.CanRead)
                {
                    m_inActive = false;
                    return false;
                }

                //  If the next item in the pipe is message delimiter,
                //  initiate termination process.
                //if (IsDelimiter(m_inboundQueue.Peek()))
                //{
                //    Msg msg = new Msg();
                //    bool ok = m_inboundPipe.Read(ref msg);
                //    Debug.Assert(ok);
                //    Delimit();
                //    return false;
                //}

                return true;
            }
        }

        public bool CanWrite
        {
            get
            {
                if (!m_outActive || m_state != State.Active)
                    return false;

                bool full = m_highWatermark > 0 && m_numberOfMessagesWritten - 
                    m_peersMsgsRead == (long)(m_highWatermark);

                if (full)
                {
                    m_outActive = false;
                    return false;
                }

                return true;
            }
        } 
        
        public bool Read(ref Message message)
        {
            if (!m_inActive || (m_state != State.Active && m_state != State.Pending))
                return false;

            if (!m_inboundQueue.Dequeue(ref message))
            {
                m_inActive = false;
                return false;
            }

            //  If delimiter was read, start termination process of the pipe.
            //if (msg.IsDelimiter)
            //{
            //    Delimit();
            //    return false;
            //}

            m_numberOfMessagesRead++;

            if (m_lowWatermark > 0 && m_numberOfMessagesRead % m_lowWatermark == 0)
                m_peer.Send(new ActivateWrite(m_numberOfMessagesRead));                

            return true;
        }

        public bool Write(ref Message message)
        {
            //  The peer does not exist anymore at this point.
            if (m_state == State.Terminating)
                return false;

            if (!CanWrite)
                return false;

            bool isPeerAwake = m_outboundQueue.Enqueue(ref message);

            m_numberOfMessagesWritten++;

            if (m_outboundQueue != null && !isPeerAwake)
                m_peer.Send(new ActivateRead());

            return true;
        }


        private void OnAcitvateRead(ActorAddress replyaddress, ActivateRead message)
        {
            if (m_inActive || (m_state != State.Active && m_state != State.Pending))
                return;
            m_inActive = true;
            m_parent.Send(new Socket.ActivateRead(this));            
        }

        private void OnAcitvateWrite(ActorAddress replyaddress, ActivateWrite message)
        {
            //  Remember the peer's message sequence number.
            m_peersMsgsRead = message.NumberOfMessagesRead;

            if (m_outActive || m_state != State.Active)
                return;
            m_outActive = true;
            m_parent.Send(new Socket.ActivateWrite(this));
        }

        
    }
}
