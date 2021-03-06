﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using NetMessage.AsyncIO;
using NetMessage.Core;

namespace NetMessage.Core
{
    public enum SendReceiveResult
    {
        Ok, ShouldTryAgain
    }

    abstract class SocketBase : IDisposable
    {
        //public const int EventIn = 1;
        //public const int EventOut = 2;

        private Socket m_socket;

        /// <summary>
        /// Initialise the socket base class. 'hint' is the opaque value passed to the
        /// transport's 'Create' function.
        /// </summary>
        /// <param name="hint"></param>
        protected SocketBase(object hint)
        {
            m_socket = (Socket) hint;
        }

        public virtual void Dispose()
        {

        }

        /// <summary>
        /// Call this function when stopping is done.
        /// </summary>
        public void Stopped()
        {
            m_socket.Stopped();     
        }

        /// <summary>
        /// Returns the AIO context associated with the socket. This function is
        /// useful when socket type implementation needs to create async objects,
        /// such as timers.
        /// </summary>
        public Context Context
        {
            get
            {
                return m_socket.Context;    
            }
        }

        public object GetOption(int option)
        {
            return m_socket.GetOptionInner((SocketOption)option);
        }

        protected internal virtual bool Stop()
        {
            return true;
        }        

        protected internal abstract void Add(IPipe pipe);
        protected internal abstract void Remove(IPipe pipe);

        protected internal abstract void In(IPipe pipe);
        protected internal abstract void Out(IPipe pipe);

        protected internal abstract SocketEvents Events { get; }

        protected internal abstract SendReceiveResult Send(Message message);
        protected internal abstract SendReceiveResult Receive(out Message message);

        protected internal abstract void SetOption(int option, object value);        
    }
}
