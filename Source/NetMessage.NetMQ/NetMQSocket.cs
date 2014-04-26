using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core;
using NetMessage.Core.Core;
using NetMessage.NetMQ.Patterns;

namespace NetMessage.NetMQ
{
    public class NetMQSocket : Socket<NetMQMessage>
    {
        private NetMQMessage m_outMessage = null;
        private NetMQMessage m_inMessage = null;

        internal NetMQSocket(SocketType<NetMQMessage> socketType) : base(socketType)
        {
        }

        protected override Core.Transport.Transport<NetMQMessage> GetTransport(string name)
        {
            return Global.GetTransport(name);
        }

        public bool IsMore
        {
            get { return m_inMessage != null; }
        }

        public void Send(byte[] bytes, bool dontWait = false)
        {
            if (m_outMessage == null)
            {
                m_outMessage = new NetMQMessage();                
            }

            m_outMessage.Append(bytes);

            SendMessage(m_outMessage,dontWait);
            m_outMessage = null;
        }

        public void Send(string message, bool dontWait = false)
        {
            Send(Encoding.ASCII.GetBytes(message), dontWait);
        }

        public void SendMore(string message)
        {
            SendMore(Encoding.ASCII.GetBytes(message));
        }

        public void SendMore(byte[] bytes)
        {
            if (m_outMessage == null)
            {
                m_outMessage = new NetMQMessage();
            }

            m_outMessage.Append(bytes);
        }

        public byte[] Receive(bool dontWait = false)
        {
            if (m_inMessage == null)
            {
                m_inMessage = ReceiveMessage(dontWait);
            }

            var frame = m_inMessage.Pop();

            var bytes = frame.ToByteArray(false);

            if (m_inMessage.FrameCount == 0)
            {
                m_inMessage = null;
            }

            return bytes;
        }

        public String ReceiveString(bool dontWait = false)
        {
            return Encoding.ASCII.GetString(Receive(dontWait));
        }        

        public static NetMQSocket CreateDealer()
        {
            return new NetMQSocket(new Dealer.DealerSocketType());
        }

        public static NetMQSocket CreateRouter()
        {
            return new NetMQSocket(new Router.RouterSocketType());
        }        
    }
}
