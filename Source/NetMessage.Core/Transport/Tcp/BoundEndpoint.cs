using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security;
using NetMessage.Core;
using NetMessage.AsyncIO;
using NetMessage.Core;
using NetMessage.Transport;
using NetMessage.Transport.Utils;
using SocketType = System.Net.Sockets.SocketType;

namespace NetMessage.Transport.Tcp
{
    class BoundEndpoint : EndpointBase
    {
        private const int USocketSourceId = 1;
        private const int AcceptConnectionSourceId = 2;

        enum State
        {
            Idle = 1,
            Active,
            StoppingAcceptSocket,
            StoppingUSocket,
            StoppingAcceptedSockets
        }

        private State m_state;

        private USocket m_usocket;

        private AcceptConnection m_acceptConnection;

        private List<AcceptConnection> m_connections;

        public BoundEndpoint(Endpoint endpoint)
            : base(endpoint)
        {
            bool ipV4Only = (bool)endpoint.GetOption(SocketOption.IPV4Only);

            // just to check if valid address
            AddressUtility.ResolveAddress(Address, ipV4Only);

            m_state = State.Idle;

            m_acceptConnection = null;
            m_connections = new List<AcceptConnection>();

            m_usocket = new USocket(USocketSourceId, this);

            StartStateMachine();
        }

        public override void Dispose()
        {
            Debug.Assert(m_state == State.Idle);

            m_usocket.Dispose();
            base.Dispose();
        }

        internal override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == StateMachine.ActionSourceId && type == StateMachine.StopAction)
            {
                m_acceptConnection.Stop();
                m_state = State.StoppingAcceptSocket;
            }

            if (m_state == State.StoppingAcceptSocket)
            {
                if (!m_acceptConnection.IsIdle)
                {
                    return;
                }

                m_acceptConnection.Dispose();
                m_acceptConnection = null;
                m_usocket.Stop();

                m_state = State.StoppingUSocket;
            }

            if (m_state == State.StoppingUSocket)
            {
                if (!m_usocket.IsIdle)
                {
                    return;
                }

                if (m_connections.Count > 0)
                {
                    foreach (AcceptConnection acceptedConnection in m_connections)
                    {
                        acceptedConnection.Stop();
                    }

                    m_state = State.StoppingAcceptedSockets;
                }
                else
                {
                    m_state = State.Idle;
                    StoppedNoEvent();
                    Stopped();
                }
            }
            else if (m_state == State.StoppingAcceptedSockets)
            {
                AcceptConnection connection = (AcceptConnection)source;
                m_connections.Remove(connection);
                connection.Dispose();

                if (m_connections.Count == 0)
                {
                    m_state = State.Idle;
                    StoppedNoEvent();
                    Stopped();
                }
            }
        }

        internal override void Handle(int sourceId, int type, StateMachine source)
        {
            switch (m_state)
            {
                case State.Idle:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case StateMachine.StartAction:
                                    StartListening();
                                    StartAccepting();
                                    m_state = State.Active;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Active:
                    switch (sourceId)
                    {
                        case AcceptConnectionSourceId:
                            if (source == m_acceptConnection)
                            {
                                switch (type)
                                {
                                    case AcceptConnection.AcceptedEvent:
                                        m_connections.Add(m_acceptConnection);
                                        m_acceptConnection = null;
                                        StartAccepting();
                                        break;
                                }
                            }

                            AcceptConnection connection = (AcceptConnection)source;

                            switch (type)
                            {
                                case AcceptConnection.ErrorEvent:
                                    connection.Stop();
                                    break;
                                case AcceptConnection.StoppedEvent:
                                    m_connections.Remove(connection);
                                    connection.Dispose();
                                    break;
                            }
                            break;
                    }

                    break;
            }
        }

        private void StartListening()
        {
            bool ipV4Only = (bool)GetOption(SocketOption.IPV4Only);
            var endpoint = AddressUtility.ResolveAddress(Address, ipV4Only);

            m_usocket.Start(ipV4Only ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            m_usocket.Bind(endpoint as IPEndPoint);

            // TODO: the backlog should be an option
            m_usocket.Listen(100);
        }

        private void StartAccepting()
        {
            Debug.Assert(m_acceptConnection == null);

            m_acceptConnection = new AcceptConnection(this, AcceptConnectionSourceId);

            m_acceptConnection.Start(m_usocket);
        }
    }
}
