using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Core;
using NetMessage.Core.Transport;
using NetMessage.Core.Transport.Utils;

namespace NetMessage.NetMQ.Tcp
{
    public class ConnectEndpoint : EndpointBase<NetMQMessage>
    {
        enum State
        {
            Idle = 1,
            Resolving,
            StoppingDNS,
            Connecting,
            Active,
            StoppingSession,
            StoppingUSocket,
            Waiting,
            StoppingBackoff,
            StoppingSessionFinal,
            Stopping
        }

        private const int USocketSourceId = 1;
        private const int ReconnectTimerSourceId = 2;
        private const int DNSSourceId = 3;
        private const int SessionSourceId = 4;

        private State m_state;
        private USocket m_usocket;

        private Backoff m_retry;

        private Session m_session;

        private DNS m_dns;
        private DNSResult m_dnsResult;

        private EndPoint m_addressEndPoint;

        public ConnectEndpoint(Endpoint<NetMQMessage> endpoint)
            : base(endpoint)
        {
            bool ipV4only = (bool)endpoint.GetOption(SocketOption.IPV4Only);

            m_addressEndPoint  = AddressUtility.ResolveAddress(endpoint.Address, ipV4only);            

            m_state = State.Idle;
            m_usocket = new USocket(USocketSourceId, this);

            int reconnectInterval = (int)endpoint.GetOption(SocketOption.ReconnectInterval);
            int reconnectIntervalMax = (int)endpoint.GetOption(SocketOption.ReconnectIntervalMax);

            if (reconnectIntervalMax == 0)
            {
                reconnectIntervalMax = reconnectInterval;
            }

            m_retry = new Backoff(ReconnectTimerSourceId, reconnectInterval, reconnectIntervalMax, this);
            m_session = new Session(SessionSourceId, this, this);
            m_dns = new DNS(DNSSourceId, this);

            StartStateMachine();
        }

        public void Stop()
        {
            StopStateMachine();
        }

        public override void Dispose()
        {
            m_dns.Dispose();
            m_session.Dispose();
            m_retry.Dispose();
            m_usocket.Dispose();
            base.Dispose();
        }

        protected override void Shutdown(int sourceId, int type, Core.AsyncIO.StateMachine source)
        {
            if (sourceId == ActionSourceId && type == StopAction)
            {
                if (!m_session.IsIdle)
                {
                    m_session.Stop();
                }

                m_state = State.StoppingSessionFinal;
            }

            if (m_state == State.StoppingSessionFinal)
            {
                if (!m_session.IsIdle)
                {
                    return;
                }

                m_retry.Stop();
                m_usocket.Stop();
                m_dns.Stop();

                m_state = State.Stopping;
            }

            if (m_state == State.Stopping)
            {
                if (!m_retry.IsIdle || !m_usocket.IsIdle || !m_dns.IsIdle)
                    return;

                m_state = State.Idle;
                StoppedNoEvent();
                Stopped();
            }
            else
            {
                // TODO: throw bad state
            }
        }

        protected override void Handle(int sourceId, int type, Core.AsyncIO.StateMachine source)
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
                                    StartResolving();
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Resolving:
                    switch (sourceId)
                    {
                        case DNSSourceId:
                            switch (type)
                            {
                                case DNS.DoneEvent:
                                    m_dns.Stop();
                                    m_state = State.StoppingDNS;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.StoppingDNS:
                    switch (sourceId)
                    {
                        case DNSSourceId:
                            switch (type)
                            {
                                case DNS.StoppedEvent:
                                    if (!m_dnsResult.Error)
                                    {
                                        int port;

                                        if (m_addressEndPoint is IPEndPoint)
                                        {
                                            port = (m_addressEndPoint as IPEndPoint).Port;
                                        }
                                        else
                                        {
                                            port = (m_addressEndPoint as DnsEndPoint).Port;
                                        }

                                        StartConnecting(new IPEndPoint(m_dnsResult.Address, port));
                                    }
                                    else
                                    {
                                        m_retry.Start();
                                        m_state = State.Waiting;
                                    }
                                    break;
                            }
                            break;

                    }
                    break;
                case State.Connecting:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ConnectedEvent:
                                    m_session.Start(m_usocket);
                                    m_state = State.Active;
                                    base.ClearError();
                                    break;
                                case USocket.ErrorEvent:
                                    base.SetError();
                                    m_usocket.Stop();
                                    m_state = State.StoppingUSocket;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Active:
                    switch (sourceId)
                    {
                        case SessionSourceId:
                            switch (type)
                            {
                                case Session.ErrorEvent:
                                    m_session.Stop();
                                    m_state = State.StoppingSession;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.StoppingSession:
                    switch (sourceId)
                    {
                        case SessionSourceId:
                            switch (type)
                            {
                                case USocket.ShutdownEvent:
                                    break;
                                case Session.StoppedEvent:
                                    m_usocket.Stop();
                                    m_state = State.StoppingUSocket;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.StoppingUSocket:
                    switch (sourceId)
                    {
                        case USocketSourceId:
                            switch (type)
                            {
                                case USocket.ShutdownEvent:
                                    break;
                                case USocket.StoppedEvent:
                                    m_retry.Start();
                                    m_state = State.Waiting;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Waiting:
                    switch (sourceId)
                    {
                        case ReconnectTimerSourceId:
                            switch (type)
                            {
                                case Backoff.TimeOutEvent:
                                    m_retry.Stop();
                                    m_state = State.StoppingBackoff;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.StoppingBackoff:
                    switch (sourceId)
                    {
                        case ReconnectTimerSourceId:
                            switch (type)
                            {
                                case Backoff.StoppedEvent:
                                    StartResolving();
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }

        private void StartResolving()
        {
            bool ipv4Only = (bool) GetOption(SocketOption.IPV4Only);
                        
            if (m_addressEndPoint is IPEndPoint)
            {
                StartConnecting(m_addressEndPoint as IPEndPoint);
            }
            else
            {
                string host = (m_addressEndPoint as IPEndPoint).Address.ToString();
                m_dns.Start(host, ipv4Only, m_dnsResult);

                m_state = State.Resolving;
            }
        }

        private void StartConnecting(IPEndPoint ipEndPoint)
        {
            try
            {
                m_usocket.Start(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (SocketException)
            {
                m_retry.Start();
                m_state = State.Waiting;        
                return;
            }

            int sendBuffer = (int)GetOption(SocketOption.SendBuffer);
            int receieBuffer = (int)GetOption(SocketOption.ReceiveBuffer);

            // TODO: use direct properties instead, more .net style
            //m_usocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, sendBuffer);
            m_usocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, receieBuffer);
            m_usocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, 1);

            m_usocket.Connect(ipEndPoint);
            m_state = State.Connecting;
        }

    }
}
