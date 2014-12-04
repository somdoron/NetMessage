using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NetMessage2.Core;
using NetMessage2.Transport;

namespace NetMessage2
{
    public abstract class Socket : Actor, IDisposable
    {        
        public class AttachPipeMessage
        {
            
        }

        public class DetachPipeMessage
        {
            
        }

        public class ActivateRead
        {
            public ActivateRead(Pipe pipe)
            {
                Pipe = pipe;
            }

            public Pipe Pipe { get; private set; }
        }

        public class ActivateWrite
        {
            public ActivateWrite(Pipe pipe)
            {
                Pipe = pipe;
            }

            public Pipe Pipe { get; private set; }
        }

        private IList<ActorAddress> m_listeners;
        private IList<ActorAddress> m_connectors;


        public Socket() : base()
        {
            m_listeners = new List<ActorAddress>();
            m_connectors = new List<ActorAddress>();

            
            // register message handlers
            Receive<AttachPipeMessage>(OnAttachPipe);
            Receive<DetachPipeMessage>(OnDetachPipe);
        }        

        public abstract SocketType SocketType { get; }

        public void Bind(string address)
        {
            ProcessMessages(0);

            Uri uri  = new Uri(address, UriKind.Absolute);

            ITransport transport = Global.GetTransport(uri.Scheme);

            if (!transport.IsSocketTypeSupported(SocketType))
            {
                throw new NotSupportedException(string.Format("Transport {0} doesn't support {1} socket type", 
                    transport.Name, SocketType.Name));
            }

            // we use temporary reply mailbox because we want to wait for the reply message
            using (ReplyMailbox replyMailbox = new ReplyMailbox())
            {
                var listenerAddress = CreateActor(transport.ListenerFactory);
                listenerAddress.Send(replyMailbox.ActorAddress, new Listener.BindMessage(uri));

                var bindResult = replyMailbox.WaitForReply<Listener.BindReplyMessage>();

                if (bindResult.Success)
                {
                    m_listeners.Add(listenerAddress);
                }
                else
                {
                    listenerAddress.Send(replyMailbox.ActorAddress, new Actor.DisposeMessage());

                    replyMailbox.WaitForReply<DisposeReplyMessage>();

                    // TODO: throw bettr exception
                    throw new Exception("Bind failed");
                }
            }
        }

        public void Connect(string address)
        {
            ProcessMessages(0);

            Uri uri = new Uri(address, UriKind.Absolute);

            ITransport transport = Global.GetTransport(uri.Scheme);

            if (!transport.IsSocketTypeSupported(SocketType))
            {
                throw new NotSupportedException(string.Format("Transport {0} doesn't support {1} socket type",
                    transport.Name, SocketType.Name));
            }

            // we use temporary reply mailbox because we want to wait for the reply message
            using (ReplyMailbox replyMailbox = new ReplyMailbox())
            {
                var connectorAddress = CreateActor(transport.ConnectorFactory);
                connectorAddress.Send(replyMailbox.ActorAddress, new Connector.ConnectMessage(uri));

                var bindResult = replyMailbox.WaitForReply<Connector.ConnectReplyMessage>();

                if (bindResult.Success)
                {
                    m_listeners.Add(connectorAddress);
                }
                else
                {
                    connectorAddress.Send(replyMailbox.ActorAddress, new Actor.DisposeMessage());

                    replyMailbox.WaitForReply<DisposeReplyMessage>();

                    // TODO: throw bettr exception
                    throw new Exception("Bind failed");
                }
            }
        }        

        private void OnAttachPipe(ActorAddress replyAddress, AttachPipeMessage attachPipeMessage)
        {
            
        }

        private void OnDetachPipe(ActorAddress replyaddress, DetachPipeMessage message)
        {

        }

        public void Dispose()
        {
            
        }
    }
}
