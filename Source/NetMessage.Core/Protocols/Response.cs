using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core;
using NetMessage.Core;

namespace NetMessage.Protocols
{
    class Response : Router
    {
        public class ResponseSocketType : SocketType
        {
            public ResponseSocketType(SocketTypeFlags flags)
                : base(SocketTypes.Response, flags)
            {
            }

            public override SocketBase Create(object hint)
            {
                return new Response(hint);
            }

            public override bool IsPeer(int socketType)
            {
                return socketType == SocketTypes.Request || socketType == SocketTypes.Dealer;
            }
        }

        //  If true, we are in process of sending the reply. If false we are
        //  in process of receiving a request.
        private bool m_sendingReply;

        private List<Frame> m_identityFrames;

        public Response(object hint)
            : base(hint)
        {
            m_sendingReply = false;
            m_identityFrames = new List<Frame>();
        }

        protected internal override SendReceiveResult Send(Message message)
        {
            if (!m_sendingReply)
            {
                throw new InvalidOperationException("Cannot send another reply");
            }
         
            // push identities frames backwards
            for (int i = m_identityFrames.Count - 1 ; i >= 0; i--)
            {                           
                message.Push(m_identityFrames[i]);
            }

            var sendResult = base.Send(message);

            if (sendResult == SendReceiveResult.Ok)
            {
                m_sendingReply = false;
                m_identityFrames.Clear();
            }
            else
            {
                // lets remove the frames we just added
                for (int i = 0; i < m_identityFrames.Count; i++)
                {
                    message.Pop();
                }             
            }

            return sendResult;
        }

        protected internal override SendReceiveResult Receive(out Message message)
        {
            if (m_sendingReply)
            {
                throw new InvalidOperationException("Cannot receive another request");
            }

            var receiveResult = base.Receive(out message);

            if (receiveResult == SendReceiveResult.ShouldTryAgain)
            {
                return SendReceiveResult.ShouldTryAgain;
            }

            while (true)
            {
                // remove the and save the identity frame
                Frame frame = message.Pop();

                if (message.FrameCount > 0)
                {                                          
                    m_identityFrames.Add(frame);

                    //  Empty message part delimits the traceback stack.
                    if (frame.MessageSize == 0)
                    {
                        break;
                    }
                }
                else
                {
                    // If the traceback stack is malformed, clear the identities (we're at end of invalid message)
                    // and receive another message
                    m_identityFrames.Clear();

                    receiveResult = base.Receive(out message);

                    if (receiveResult == SendReceiveResult.ShouldTryAgain)
                    {
                        return SendReceiveResult.ShouldTryAgain;
                    }
                }
            }

            return SendReceiveResult.Ok;
        }


        protected internal override SocketEvents Events
        {
            get
            {
                return base.Events & (m_sendingReply ? SocketEvents.Out : SocketEvents.In);
            }
        }       
    }
}
