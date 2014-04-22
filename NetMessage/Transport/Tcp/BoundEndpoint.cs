using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetMessage.AsyncIO;
using NetMessage.Core;
using NetMessage.Transport.Utils;
using NetSocket = System.Net.Sockets.Socket;
using SocketType = System.Net.Sockets.SocketType;

namespace NetMessage.Transport.Tcp
{
    public class BoundEndpoint : EndpointBase
    {
        enum State
        {
            Idle = 1,
            Active,
            StoppingAcceptedSocket,
            //StoppingSocket,
            StoppingAcceptedSockets
        }

        private State m_state;

        private NetSocket m_netSocket;

        private NetSocket m_acceptedConnection;

        private List<AcceptedConnection> m_connections;

        public BoundEndpoint(Endpoint endpoint)
            : base(endpoint)
        {
            string address = endpoint.Address;
            
            bool ip4Only = (bool)endpoint.GetOption(SocketOption.IPV4Only);

            // just to check if valid address
            AddressUtility.ResolveAddress(Address, ipV4Only);

            m_state = StateMachine.State.Idle;

            m_acceptedConnection = null;
            m_connections = new List<AcceptedConnection>();

            m_netSocket = new NetSocket(
                ip4Only ? AddressFamily.InterNetwork :  AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

            StartStateMachine();
        }

        public override void Dispose()
        {
            Debug.Assert(m_state == StateMachine.State.Idle);

            m_netSocket.Dispose();
            base.Dispose();
        }

        public void Stop()
        {
            StopStateMachine();
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == StateMachine.ActionSourceId && type == StateMachine.StopAction)
            {
                m_acceptedConnection.Stop();
                m_state = State.StoppingAcceptedSocket;
            }

            if (m_state == State.StoppingAcceptedSocket)
            {
                if (!m_acceptedConnection.IsIdle)
                {
                    return;
                }

                m_acceptedConnection.Dispose();
                m_acceptedConnection = null;
                m_netSocket.Dispose();

                if (m_connections.Count > 0)
                {
                    foreach (AcceptedConnection acceptedConnection in m_connections)
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
                AcceptedConnection connection= (AcceptedConnection)source;
                m_connections.Remove(connection)
                connection.Dispose();

                if (m_connections.Count == 0)
                {
                    m_state = State.Idle;
                    StoppedNoEvent();
                    Stopped();
                }
            }
        }

        protected override void Handle(int sourceId, int type, StateMachine source)
        {
            
        }

        private void StartListening()
        {            
            bool ipV4Only = GetOption(SocketOption.IPV4Only);
            var endpoint = AddressUtility.ResolveAddress(Address, ipV4Only);

            m_netSocket.Bind(endpoint);

            // TODO: the backlog should be an option
            m_netSocket.Listen(100);            
        }

        private void StartAccepting()
        {
            Debug.Assert(m_acceptedConnection == null);

            
        }
    }
}
