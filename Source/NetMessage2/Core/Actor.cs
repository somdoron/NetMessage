using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage2.Core
{    
    public delegate void MessageHandler<T>(ActorAddress replyAddress, T message);

    public class Actor
    {
        public class DisposeMessage
        {
            
        }

        public class DisposeReplyMessage
        {
            
        }

        private readonly ActorAddress m_parent;
        private IMailbox m_mailbox;

        /// <summary>
        /// Child actor
        /// </summary>
        /// <param name="parent"></param>
        public Actor(ActorAddress parent, IMailbox mailbox)
        {
            m_parent = parent;
            m_mailbox = mailbox;
        }

        /// <summary>
        /// This is root actor and create it is own mailbox and don't use a worker
        /// </summary>
        internal Actor()
        {            
            m_parent = null;
            m_mailbox = new SocketMailbox();
        }

        /// <summary>
        /// Create a new actor that uses the same mailbox as parent
        /// </summary>
        /// <param name="factory"></param>
        /// <returns></returns>
        protected ActorAddress CreateActor(Func<Actor, ActorAddress, IMailbox> factory)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a new actor that pick a worker
        /// </summary>
        /// <param name="factory"></param>
        /// <returns></returns>
        protected ActorAddress CreateActorOnWorker(Func<Actor, ActorAddress, IMailbox> factory)
        {
            throw new NotImplementedException();
        }
        
        protected void Receive<T>(MessageHandler<T> handler)
        {
            
        }

        internal void ProcessMessages(int timeout)
        {

        }              
    }
}
