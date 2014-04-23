﻿using System;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Core;

namespace NetMessage.Core.Transport
{
    public abstract class EndpointBase<T> : StateMachine where T: MessageBase
    {
        private Endpoint<T> m_endpoint;

        public EndpointBase(Endpoint<T> endpoint) : base(endpoint.Context)
        {
            m_endpoint = endpoint;
        }        

        public string Address
        {
            get
            {
                return m_endpoint.Address;
            }
        }

        public Endpoint<T> Endpoint
        {
            get { return m_endpoint; }
        }

        public virtual void Dispose()
        {
            
        }

        public void Stop()
        {
            base.StopStateMachine();
        }

        public void Stopped()
        {
            m_endpoint.Stopped();
        }
       
        public object GetOption(SocketOption option)
        {
            return m_endpoint.GetOption(option);
        }

        public bool IsPeer(int socketType)
        {
            return m_endpoint.IsPeer(socketType);
        }

        public void SetError()
        {
            m_endpoint.SetError();
        }

        public void ClearError()
        {
            m_endpoint.ClearError();    
        }        
    }
}
