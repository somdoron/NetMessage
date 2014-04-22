﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetSocket = System.Net.Sockets.Socket;

namespace NetMessage.Core.AsyncIO
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
            Idle = 1,
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

        public USocket(StateMachine owner)
            : base(SourceId, owner)
        {
            m_state = State.Idle;

            m_in = new AsyncOperation(InSourceId, this);
            m_out = new AsyncOperation(OutSourceId, this);

            m_establishedEvent = new StateMachineEvent();
            m_sendEvent = new StateMachineEvent();
            m_receivedEvent = new StateMachineEvent();
            m_errorEvent = new StateMachineEvent();
            m_acceptSocket = null;
        }

        public override void Dispose()
        {
            Debug.Assert(m_state == State.Idle);

            m_in.Dispose();
            m_out.Dispose();

            if (m_socket != null)
            {
                try
                {
                    m_socket.Dispose();
                }
                catch
                {
                                        
                }
            }

            base.Dispose();
        }

        public bool IsIdle
        {
            get
            {
                return true;
            }
        }

        public void Start(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
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

            m_socket.SetSocketOption(level, name, value);
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

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == StateMachine.ActionSourceId && type == StateMachine.StopAction)
            {
                // Socket in ACCEPTING state cannot be closed.
                // Stop the socket being accepted first. 
                Debug.Assert(m_state != State.Accepting);

                if (m_state == State.Idle)
                {
                    // the socket already idle, do nothing   
                }
                else if (m_state == State.Done)
                {
                    m_state = State.Idle;
                    Stopped(StoppedEvent);
                }
                else if (m_state == State.Starting || m_state == State.Accepted || m_state == State.Listening)
                {
                    if (CloseSocket())
                    {
                        m_state = State.Idle;
                        Stopped(StoppedEvent);
                    }
                    else
                    {
                        m_state = State.Stopping;
                    }
                }
                else if (m_state == State.BeingAccepted)
                {
                    m_acceptSocket.Action(CancelAction);
                    
                    // will close the socket
                    CloseSocket();

                    m_state = State.StoppingAccept;
                }
                else if (m_state == State.CancellingIO)
                {
                    m_state = State.Stopping;
                }
                else
                {
                    // Notify our parent that pipe socket is shutting down
                    base.Raise(m_errorEvent, ShutdownEvent);

                    //  In all remaining states we'll simply cancel all overlapped
                    //   operations.                                        
                    if (CloseSocket())
                    {
                        m_state = State.Idle;
                        Stopped(StoppedEvent);
                    }
                    else
                    {
                        m_state = State.Stopping;
                    }
                }
            }
            else if (m_state == State.StoppingAccept)
            {
                Debug.Assert(sourceId == StateMachine.ActionSourceId && type == DoneAction);

                m_state = State.Idle;
                Stopped(StoppedEvent);
            }
            else if (m_state == State.Stopping)
            {
                if (m_in.IsIdle && m_out.IsIdle)
                {
                    m_state = State.Idle;
                    Stopped(StoppedEvent);
                }
            }
        }

        private bool CloseSocket()
        {
            try
            {
                // the only way to cancel async operation in .net is to close the socket
                m_socket.Close();
            }
            catch (SocketException ex)
            {
                Debug.Assert(false, ex.ToString());
            }

            return m_in.IsIdle && m_out.IsIdle;
        }

        protected override void Handle(int sourceId, int type, StateMachine source)
        {
            // TODO: handle all bad actions, state, source

            switch (m_state)
            {
                case State.Idle:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case StateMachine.StartAction:
                                    m_state = State.Starting;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Starting:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case ListenAction:
                                    m_state = State.Listening;
                                    break;
                                case ConnectAction:
                                    m_state = State.Connecting;
                                    break;
                                case BeingAcceptedAction:
                                    m_state = State.BeingAccepted;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.BeingAccepted:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case DoneAction:
                                    m_state = State.Accepted;
                                    Raise(m_establishedEvent, AcceptedEvent);
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Accepted:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case ActivateAction:
                                    m_state = State.Active;
                                    break;
                            }
                            break;
                    }
                    break;

                case State.Connecting:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case DoneAction:
                                    m_state = State.Active;
                                    Raise(m_establishedEvent, ConnectedEvent);
                                    break;
                                case ErrorAction:
                                    CloseAndDisposeSocket();
                                    m_state = State.Done;
                                    Raise(m_errorEvent, ErrorEvent);
                                    break;
                            }
                            break;
                        case OutSourceId:
                            switch (type)
                            {
                                case AsyncOperation.DoneEvent:
                                    m_state = State.Active;
                                    Raise(m_establishedEvent, ConnectedEvent);
                                    break;
                                case AsyncOperation.ErrorEvent:
                                    CloseAndDisposeSocket();
                                    m_state = State.Done;
                                    Raise(m_errorEvent, ErrorEvent);
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Active:
                    switch (sourceId)
                    {
                        case InSourceId:
                            switch (type)
                            {
                                case AsyncOperation.DoneEvent:
                                    Raise(m_receivedEvent, ReceivedEvent);
                                    break;
                                case AsyncOperation.ErrorEvent:
                                    if (CloseSocket())
                                    {
                                        Raise(m_errorEvent, ShutdownEvent);
                                        m_state = State.Done;
                                    }
                                    else
                                    {
                                        m_state = State.CancellingIO;
                                    }
                                    break;
                            }
                            break;
                        case OutSourceId:
                            switch (type)
                            {
                                case AsyncOperation.DoneEvent:
                                    Raise(m_sendEvent, SentEvent);
                                    break;
                                case AsyncOperation.ErrorEvent:
                                    if (CloseSocket())
                                    {
                                        Raise(m_errorEvent, ShutdownEvent);
                                        m_state = State.Done;
                                    }
                                    else
                                    {
                                        m_state = State.CancellingIO;
                                    }
                                    break;
                            }
                            break;
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case ErrorAction:
                                    if (CloseSocket())
                                    {
                                        Raise(m_errorEvent, ShutdownEvent);
                                        m_state = State.Done;
                                    }
                                    else
                                    {
                                        m_state = State.CancellingIO;
                                    }
                                    break;
                            }
                            break;
                    }
                    break;
                case State.CancellingIO:
                    switch (sourceId)
                    {
                        case InSourceId:
                        case OutSourceId:
                            if (m_in.IsIdle && m_out.IsIdle)
                            {
                                m_state = State.Done;
                            }
                            break;
                    }
                    break;
                case State.Done:
                    // TODO: throw exception bad sourceb
                    break;
                case State.Listening:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case AcceptAction:
                                    m_state = State.Accepting;
                                    break;
                            }
                            break;
                    }
                    break;

                case State.Accepting:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case DoneAction:
                                    m_state = State.Listening;
                                    break;
                                case CancelAction:                                    
                                    m_state = State.Cancelling;
                                    break;
                            }
                            break;
                        case InSourceId:
                            switch (type)
                            {
                                case AsyncOperation.DoneEvent:
                                    m_acceptSocket.m_state = State.Accepted;

                                    m_acceptSocket.Raise(m_establishedEvent, AcceptedEvent);

                                    m_acceptSocket.m_acceptSocket = null;
                                    m_acceptSocket = null;

                                    m_state = State.Listening;

                                    break;
                            }
                            break;

                    }
                    break;
                case State.Cancelling:
                    switch (sourceId)
                    {
                        case InSourceId:

                            switch (type)
                            {
                                case AsyncOperation.DoneEvent:
                                case AsyncOperation.ErrorEvent:
                                    m_state = State.Listening;
                                    m_acceptSocket.Action(DoneAction);
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }

        private void CloseAndDisposeSocket()
        {
            try
            {
                m_socket.Close();
            }
            catch (SocketException ex)
            {
                Debug.Assert(false, ex.ToString());
            }

            try
            {
                m_socket.Dispose();
            }
            catch (Exception ex)
            {
                Debug.Assert(false, ex.ToString());
            }

            m_socket = null;
        }

        public void SwapOwner(StateMachine owner)
        {

        }
    }
}
