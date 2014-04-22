using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetSocket = System.Net.Sockets.Socket;

namespace NetMessage.AsyncIO
{
    public class USocket : StateMachine, IDisposable
    {
        /// <summary>
        /// Maximum number of iovecs that can be passed to nn_usock_send function.
        /// </summary>
        public const int MaxIOCount = 3;

        /// <summary>
        /// Size of the buffer used for batch-reads of inbound data. To keep the
        /// performance optimal make sure that this value is larger than network MTU.
        /// </summary>
        public const int BatchSize = 2048;

        enum State
        {
            Idle=1,
            Starting,
            BeingAccepted,
            Accepted,
            Connecting,
            Active,
            CancellingIO,
            Done,
            Listening,
            Accepting,
            Cancelling,
            Stopping,
            StoppingAccept
        }

        public const int SourceId = 30;
        private const int InSourceId = 31;
        private const int OutSourceId = 32;

        public const int ConnectedEvent = 1;
        public const int AcceptedEvent = 1;
        public const int SentEvent = 1;
        public const int ReceivedEvent = 1;
        public const int ErrorEvent = 1;
        public const int AcceptErrorEvent = 1;
        public const int StoppedEvent = 1;
        public const int ShutdownEvent = 1;

        private const int AcceptAction = 1;
        private const int BeingAcceptedAction = 2;
        private const int CancelAction = 3;
        private const int ListenAction = 4;
        private const int ConnectAction = 5;
        private const int ActivateAction = 6;
        private const int DoneAction = 7;
        private const int ErrorAction = 8;

        private State m_state;

        private NetSocket m_socket;
        private AsyncOperation m_in;
        private AsyncOperation m_out;        

        private StateMachineEvent m_establishedEvent;
        private StateMachineEvent m_sendEvent;
        private StateMachineEvent m_receivedEvent;
        private StateMachineEvent m_errorEvent;

        private USocket m_acceptSocket;

        public USocket(StateMachine owner) : base(SourceId, owner)
        {    
            m_state = State.Idle;
            
            m_in = new AsyncOperation(InSourceId, this);
            m_out = new AsyncOperation(OutSourceId, this);

            m_domain = -1;
            m_type = -1;
            m_protocol = -1;

            m_establishedEvent = new StateMachineEvent();
            m_sendEvent = new StateMachineEvent();
            m_receivedEvent = new StateMachineEvent();
            m_errorEvent = new StateMachineEvent();
            m_acceptSocket = null;
        }

        public override void Dispose()
        {
            Debug.Assert(m_state == StateMachine.State.Idle);

            m_inEventArgs.Dispose();
            m_outEventArgs.Dispose();

            base.Dispose();
        }

        public bool IsIdle
        {
            get
            {
                
            }
        }

        public void Start(AddressFamily  addressFamily,SocketType socketType , ProtocolType protocolType)
        {
            m_socket = new NetSocket(addressFamily, socketType, protocolType);
            base.StartStateMachine();
        }

        public void Stop()
        {
            base.StopStateMachine();
        }

        public void SetSocketOption(SocketOptionLevel level, SocketOptionName name, object value)
        {
            Debug.Assert(m_state == State.Starting || m_state == State.Accepted);

            m_socket.SetSocketOption(level,name,value);
        }

        public void Bind(EndPoint endPoint)
        {
            Debug.Assert(m_state == State.Starting);

            m_socket.ExclusiveAddressUse = true;

            m_socket.Bind(endPoint);
        }

        public void Listen(int backlog)
        {
            Debug.Assert(m_state == State.Starting);

            m_socket.Listen(backlog);

            Action(ListenAction);
        }

        /// <summary>
        /// Accept a new connection from a listener. When done, NN_USOCK_ACCEPTED
        /// event will be delivered to the accepted socket. To cancel the operation,
        /// stop the socket being accepted. Listening socket should not be stopped
        /// while accepting a new socket is underway.
        /// </summary>
        public void Accept(USocket listener)
        {
            Start(listener.m_socket.AddressFamily, listener.m_socket.SocketType, listener.m_socket.ProtocolType);
            
            listener.Action(AcceptAction);
            Action(BeingAcceptedAction);

            m_in.SocketAsyncEventArgs.AcceptSocket = m_socket;

            SocketError socketError;
            
            try
            {
                bool isPending = listener.m_socket.AcceptAsync(m_in.SocketAsyncEventArgs);

                if (isPending)
                {
                    socketError = SocketError.IOPending;
                }
                else
                {
                    socketError = m_in.SocketAsyncEventArgs.SocketError;
                }
            }
            catch (SocketException ex)
            {
                socketError = ex.SocketErrorCode;
            }

            if (socketError == SocketError.Success)
            {
                listener.Action(DoneAction);
                Action(DoneAction);                
            }
            else if (socketError != SocketError.IOPending)
            {
                listener.Action(ErrorAction);
                Action(ErrorAction);
            }
            else
            {
                m_acceptSocket = listener;
                listener.m_acceptSocket = this;    
            }            
        }

        /// <summary>
        /// When all the tuning is done on the accepted socket, call this function
        /// to activate standard data transfer phase.
        /// </summary>
        public void Activate()
        {
            Action(ActivateAction);
        }

        /// <summary>
        /// Start connecting. Prior to this call the socket has to be bound to a local
        /// address. When connecting is done NN_USOCK_CONNECTED event will be reaised.
        /// If connecting fails NN_USOCK_ERROR event will be raised.
        /// </summary>
        public void Connect(EndPoint endpoint)
        {
            Debug.Assert(m_state == State.Starting);

            Action(ConnectAction);

            m_out.SocketAsyncEventArgs.RemoteEndPoint = endpoint;           

            SocketError socketError;

            try
            {
                bool isPending = m_socket.ConnectAsync(m_out.SocketAsyncEventArgs);

                if (isPending)
                {
                    socketError = SocketError.IOPending;                                       
                }
                else
                {
                    socketError = m_out.SocketAsyncEventArgs.SocketError;
                }
            }
            catch (SocketException ex)
            {
                socketError = ex.SocketErrorCode;
            }

            if (socketError == SocketError.Success)
            {
                Action(DoneAction);                
            }
            else if (socketError != SocketError.IOPending)
            {
                Action(ErrorAction);
            }                                  
        }

        public void Send(ArraySegment<byte>[] items)
        {
            Debug.Assert(m_state == State.Active);

            if (items.Length == 1)
            {
                m_out.SocketAsyncEventArgs.SetBuffer(items[0].Array, items[0].Offset, items[0].Count);
            }
            else
            {
                m_out.SocketAsyncEventArgs.BufferList = items.ToList();
            }

            SocketError socketError;

            try
            {
                bool isPending = m_socket.SendAsync(m_out.SocketAsyncEventArgs);

                if (isPending)
                {
                    socketError = SocketError.IOPending;
                }
                else
                {
                    socketError = m_out.SocketAsyncEventArgs.SocketError;
                }
            }
            catch (SocketException ex)
            {
                socketError = ex.SocketErrorCode;
            }

            if (socketError != SocketError.Success && socketError != SocketError.IOPending)
            {
                Action(ErrorAction);   
            }
        }

        public void Receive(byte[] buffer, int offset, int count)
        {
            Debug.Assert(m_state == State.Active);

            m_in.SocketAsyncEventArgs.SetBuffer(buffer, offset, count);

            SocketError socketError;

            try
            {
                bool isPending = m_socket.ReceiveAsync(m_in.SocketAsyncEventArgs);

                if (isPending)
                {
                    socketError = SocketError.IOPending;
                }
                else
                {
                    socketError = m_in.SocketAsyncEventArgs.SocketError;
                }
            }
            catch (SocketException ex)
            {
                socketError = ex.SocketErrorCode;
            }

            if (socketError != SocketError.Success && socketError != SocketError.IOPending)
            {
                Action(ErrorAction);
            }
        }

        public void SwapOwner(StateMachine owner)
        {

        }                              
       

        

      

       

        protected override void Handle(int sourceId, int type, StateMachine source)
        {
            throw new NotImplementedException();
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            throw new NotImplementedException();
        }
    }
}
