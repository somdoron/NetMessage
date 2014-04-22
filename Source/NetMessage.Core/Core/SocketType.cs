using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core;

namespace NetMessage.Core.Core
{
    [Flags]
    public enum SocketTypeFlags
    {
        None = 0,
        NoReceive = 1,
        NoSend = 2,
    }

    public abstract class SocketType
    {        
        private int m_protocol;
        private SocketTypeFlags m_flags;

        protected SocketType(int protocol, SocketTypeFlags flags)
        {            
            m_protocol = protocol;
            m_flags = flags;
        }

        public abstract SocketBase Create(object hint);

        public abstract bool IsPeer(int socketType);

        public SocketTypeFlags Flags
        {
            get { return m_flags; }
        }

        public int Protocol
        {
            get { return m_protocol; }
        }
    
        public bool CanSend
        {
            get
            {
                return !Flags.HasFlag(SocketTypeFlags.NoSend);
            }
        }

        public bool CanReceive
        {
            get
            {
                return !Flags.HasFlag(SocketTypeFlags.NoSend);
            }
        }
    }
}
