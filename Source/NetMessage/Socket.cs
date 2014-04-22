using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Core;
using NetMessage.Core.Transport;
using NetMessage.Core.Utils;
using NetMessage.Core;
using NetMessage.Transport;

namespace NetMessage.Core
{
    public class Socket : StateMachine
    {
        public const int StoppedAction = 1;        

        enum SocketState
        {
            Init = 1,
            Active,
            StoppingEndpoints,
            Stopping,
        }
        
        [Flags]
        enum SocketFlags
        {
            In = 1,
            Out = 2
        }
        
        private SocketState m_state;

        private SocketBase m_socketBase;
        private SocketType m_socketType;

        private SocketFlags m_flags;

        private Signaler m_sendSignaler;
        private Signaler m_receiveSignaler;
        private AutoResetEvent m_terminateEvent;

        private List<Endpoint> m_endpoints;
        private List<Endpoint> m_shutingdownEndpoints;

        /// <summary>
        /// Next endpoint ID to assign to a new endpoint.
        /// </summary>
        private int m_endpointId;

        private int m_linger;
        private int m_sendBuffer;
        private int m_receiveBuffer;
        private int m_sendTimeout;
        private int m_receiveTimeout;
        private int m_reconnectInterval;
        private int m_reconnectIntervalMax;

        private EndpointOptions m_endpointTemplate;

        private Dictionary<string, OptionSet> m_optionSets;

        public Socket(SocketType socketType)
            : base(new Context())
        {
            // Make sure that at least one message direction is supported.
            Debug.Assert(((socketType.Flags & SocketTypeFlags.NoReceive) == 0) ||
                ((socketType.Flags & SocketTypeFlags.NoSend) == 0));

            Context.SetOnLeave(OnLeave);

            m_state = SocketState.Active;

            if (!socketType.Flags.HasFlag(SocketTypeFlags.NoSend))
            {
                m_sendSignaler = new Signaler();
            }
            else
            {
                m_sendSignaler = null;
            }

            if (!socketType.Flags.HasFlag(SocketTypeFlags.NoReceive))
            {
                m_receiveSignaler = new Signaler();
            }
            else
            {
                m_receiveSignaler = null;
            }

            m_terminateEvent = new AutoResetEvent(false);

            m_flags = 0;
            m_endpoints = new List<Endpoint>();
            m_shutingdownEndpoints = new List<Endpoint>();
            m_endpointId = 1;

            m_optionSets = new Dictionary<string, OptionSet>();

            // Default values for socket level
            m_linger = 1000;
            m_sendBuffer = 128 * 1024;
            m_receiveBuffer = 128 * 1024;
            m_sendTimeout = -1;
            m_receiveTimeout = -1;
            m_reconnectInterval = 100;
            m_reconnectIntervalMax = 0;

            m_endpointTemplate = new EndpointOptions();

            m_endpointTemplate.SendPriority = 8;
            m_endpointTemplate.ReceivePriority = 8;
            m_endpointTemplate.IP4Only = true;

            m_socketBase = socketType.Create(this);
            m_socketType = socketType;

            Context.Enter();
            try
            {
                base.StartStateMachine();
            }
            finally
            {
                Context.Leave();
            }
        }

        internal EndpointOptions EndpointTemplate
        {
            get { return m_endpointTemplate; }
        }

        public override void Dispose()
        {
            Context.Enter();
            try
            {
                base.StopStateMachine();
            }
            finally
            {
                Context.Leave();
            }

            m_terminateEvent.WaitOne();

            // The thread that posted the semaphore can still have the ctx locked
            // for a short while. By simply entering the context and exiting it
            // immediately we can be sure that the thread in question have already
            // exited the context.
            Context.Enter();
            Context.Leave();

            // Deallocate the resources.
            base.StoppedNoEvent();
            base.Dispose();
            m_terminateEvent.Dispose();
            m_shutingdownEndpoints = null;
            m_endpoints = null;
            Context.Dispose();

            foreach (var optionSet in m_optionSets.Values)
            {
                optionSet.Dispose();
            }

            m_optionSets.Clear();
        }

        /// <summary>
        /// Called by sockbase when stopping is done.
        /// </summary>
        internal void Stopped()
        {
            StoppedEvent.StateMachine = this;
            StoppedEvent.SourceId = StateMachine.ActionSourceId;
            StoppedEvent.Source = null;
            StoppedEvent.Type = StoppedAction;
            Context.Raise(StoppedEvent);
        }

        internal bool IsPeer(int socketType)
        {
            return m_socketType.IsPeer(socketType);
        }

        public void SetTransportOption(string name, int option, object value)
        {
            Context.Enter();
            try
            {
                OptionSet optionSet = GetOptionSet(name);

                if (optionSet != null)
                {
                    optionSet.SetOption(option, value);
                }
                else
                {
                    throw new ArgumentException("Unknown transport", "level");
                }
            }
            finally
            {
                Context.Leave();
            }
        }

        public void SetPatternOption(int option, object value)
        {
            Context.Enter();
            try
            {
                m_socketBase.SetOption(option, value);
            }
            finally
            {
                Context.Leave();
            }
        }

        public void SetSocketOption(SocketOption option, object value)
        {
            Context.Enter();
            try
            {
                SetOptionInner(option, value);
            }
            finally
            {
                Context.Leave();
            }
        }

        private void SetOptionInner(SocketOption option, object value)
        {
            switch (option)
            {
                case SocketOption.Linger:
                    m_linger = (int)value;
                    break;
                case SocketOption.SendBuffer:
                    int sendBuffer = (int)value;
                    if (sendBuffer <= 0)
                        throw new ArgumentOutOfRangeException("value", "Send buffer size must be greater than zero");
                    m_sendBuffer = sendBuffer;
                    break;
                case SocketOption.ReceiveBuffer:
                    int receiveBuffer = (int)value;
                    if (receiveBuffer <= 0)
                        throw new ArgumentOutOfRangeException("value", "Receive buffer size must be greater than zero");
                    m_receiveBuffer = receiveBuffer;
                    break;
                case SocketOption.SendTimeout:
                    m_sendTimeout = (int)value;
                    break;
                case SocketOption.ReceiveTimeout:
                    m_receiveTimeout = (int)value;
                    break;
                case SocketOption.ReconnectInterval:
                    int interval = (int)value;
                    if (interval < 0)
                        throw new ArgumentOutOfRangeException("value", "Reconnect Interval must be greater or equal to zero");
                    m_reconnectInterval = interval;
                    break;
                case SocketOption.ReconnectIntervalMax:
                    int intervalMax = (int)value;
                    if (intervalMax < 0)
                        throw new ArgumentOutOfRangeException("value", "Reconnect Interval Max must be greater or equal to zero");
                    m_reconnectIntervalMax = intervalMax;
                    break;
                case SocketOption.SendPriority:
                    int sendPriority = (int)value;
                    if (sendPriority < 1 || sendPriority > 16)
                        throw new ArgumentOutOfRangeException("value", "Priority must be between 1 and 16");
                    m_endpointTemplate.SendPriority = sendPriority;
                    break;
                case SocketOption.ReceivePriority:
                    int receivePriority = (int)value;
                    if (receivePriority < 1 || receivePriority > 16)
                        throw new ArgumentOutOfRangeException("value", "Priority must be between 1 and 16");
                    m_endpointTemplate.ReceivePriority = receivePriority;
                    break;
                case SocketOption.IPV4Only:
                    m_endpointTemplate.IP4Only = (bool)value;
                    break;
                default:
                    throw new ArgumentException("Unknown protocol option", "option");
                    break;
            }
        }

        public object GetTransportOption(string name, int option)
        {
            Context.Enter();
            try
            {

                OptionSet optionSet = GetOptionSet(name);

                if (optionSet != null)
                {
                    return optionSet.GetOption(option);
                }
                else
                {
                    throw new ArgumentException("Unknown transport", "level");
                }
            }
            finally
            {
                Context.Leave();
            }
        }

        public object GetPatternOption(int option)
        {
            Context.Enter();
            try
            {
                return m_socketBase.GetOption(option);
            }
            finally
            {
                Context.Leave();
            }
        }

        public object GetSocketOption(SocketOption option)
        {
            Context.Enter();
            try
            {
                return GetOptionInner(option);
            }
            finally
            {
                Context.Leave();
            }
        }

        internal object GetOptionInner( SocketOption option)
        {
            switch (option)
            {
                case SocketOption.Type:
                    return m_socketType.Protocol;
                case SocketOption.Linger:
                    return m_linger;
                case SocketOption.SendBuffer:
                    return m_sendBuffer;
                case SocketOption.ReceiveBuffer:
                    return m_receiveBuffer;
                case SocketOption.SendTimeout:
                    return m_sendTimeout;
                case SocketOption.ReceiveTimeout:
                    return m_receiveTimeout;
                case SocketOption.ReconnectInterval:
                    return m_reconnectInterval;
                case SocketOption.ReconnectIntervalMax:
                    return m_reconnectIntervalMax;
                case SocketOption.SendPriority:
                    return m_endpointTemplate.SendPriority;
                case SocketOption.ReceivePriority:
                    return m_endpointTemplate.ReceivePriority;
                case SocketOption.IPV4Only:
                    return m_endpointTemplate.IP4Only;
                default:
                    throw new ArgumentException("Unknown protocol option", "option");
                    break;
            }
        }

        public void Bind(string address)
        {
            CreateEndpoint(true, address);
        }

        public void Connect(string address)
        {
            CreateEndpoint(false, address);
        }

        private void CreateEndpoint(bool bind, string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentNullException("address");
            }

            int delimiterIndex = address.IndexOf("://");

            if (delimiterIndex == -1)
            {
                throw new ArgumentException("invalid address", "address");
            }

            string protocol = address.Substring(0, delimiterIndex);
            address = address.Substring(delimiterIndex + 3);

            Transport.Transport transport = Global.GetTransport(protocol);

            if (transport == null)
            {
                throw new NotSupportedException(string.Format("Transport {0} is not supported", protocol));
            }

            AddEndpoint(transport, bind, address);
        }

        private int AddEndpoint(Transport.Transport transport, bool bind, string address)
        {
            Context.Enter();
            try
            {
                int id = m_endpointId;

                Endpoint endpoint = new Endpoint(this, id, transport, bind, address);
                endpoint.Start();

                // Increase the endpoint ID for the next endpoint.
                m_endpointId++;

                m_endpoints.Add(endpoint);
                return id;
            }
            finally
            {
                Context.Leave();
            }
        }

        private void RemoveEndpoint(int endpointId)
        {
            Context.Enter();
            try
            {
                Endpoint endpoint = m_endpoints.FirstOrDefault(ep => ep.Id == endpointId);

                if (endpoint != null)
                {
                    m_endpoints.Remove(endpoint);
                }
                else
                {
                    throw new ArgumentException("Endpoint with endpointId doesn't exist", "endpointId");
                }
            }
            finally
            {
                Context.Leave();
            }
        }

        public void SendMessage(Message message, bool dontWait = false)
        {
            if (!m_socketType.CanSend)
                throw new NotSupportedException("Socket type doesn't support sending");

            Context.Enter();
            try
            {
                Stopwatch stopwatch = new Stopwatch();

                int timeout = -1;
                int deadline = -1;

                if (m_sendTimeout >= 0)
                {
                    stopwatch.Start();
                    timeout = m_sendTimeout;
                    deadline = m_sendTimeout;
                }

                while (true)
                {
                    var sendResult = m_socketBase.Send(message);

                    // finish the message if the message sent successfully
                    if (sendResult == SendReceiveResult.Ok)
                        return;

                    if (dontWait)
                    {
                        throw new AgainException();
                    }

                    // we are leaving the context before the wait operation but we must enter the context after the wait is completed
                    Context.Leave();

                    bool isSignalled;
                    try
                    {
                        isSignalled = m_sendSignaler.Wait(timeout);
                    }
                    finally
                    {
                        Context.Enter();
                    }

                    if (!isSignalled)
                    {
                        throw new AgainException();
                    }

                    m_flags |= SocketFlags.Out;

                    if (m_sendTimeout >= 0)
                    {
                        long elapsed = stopwatch.ElapsedMilliseconds;

                        timeout = (int)(elapsed > deadline ? 0 : deadline - elapsed);
                    }
                }
            }
            finally
            {
                Context.Leave();
            }
        }

        public Message ReceiveMessage(bool dontWait = false)
        {
            if (!m_socketType.CanReceive)
                throw new NotSupportedException("Socket type doesn't support receiving");

            Message message;

            Context.Enter();
            try
            {
                Stopwatch stopwatch = new Stopwatch();

                int timeout = -1;
                int deadline = -1;

                if (m_receiveTimeout >= 0)
                {
                    stopwatch.Start();
                    timeout = m_sendTimeout;
                    deadline = m_sendTimeout;
                }

                while (true)
                {
                    var receiveResult = m_socketBase.Receive(out message);

                    // finish the method if the message received successfully
                    if (receiveResult == SendReceiveResult.Ok)
                        return message;

                    if (dontWait)
                    {
                        throw new AgainException();
                    }

                    // we are leaving the context before the wait operation but we must enter the context after the wait is completed
                    Context.Leave();

                    bool isSignalled;
                    try
                    {
                        isSignalled = m_receiveSignaler.Wait(timeout);
                    }
                    finally
                    {
                        Context.Enter();
                    }

                    if (!isSignalled)
                    {
                        throw new AgainException();
                    }

                    m_flags |= SocketFlags.In;

                    if (m_receiveTimeout >= 0)
                    {
                        long elapsed = stopwatch.ElapsedMilliseconds;

                        timeout = (int)(elapsed > deadline ? 0 : deadline - elapsed);
                    }
                }
            }
            finally
            {
                Context.Leave();
            }
        }

        internal void Add(IPipe pipe)
        {
            m_socketBase.Add(pipe);
        }

        internal void Remove(IPipe pipe)
        {
            m_socketBase.Remove(pipe);
        }

        private void OnLeave(Context context)
        {
            if (m_state != SocketState.Active)
                return;

            SocketEvents events = m_socketBase.Events;

            if (m_socketType.CanReceive)
            {
                if (events.HasFlag(SocketEvents.In))
                {
                    if (!m_flags.HasFlag(SocketFlags.In))
                    {
                        m_flags |= SocketFlags.In;
                        m_receiveSignaler.Signal();
                    }
                }
                else
                {
                    if (m_flags.HasFlag(SocketFlags.In))
                    {
                        m_flags &= ~SocketFlags.In;
                        m_receiveSignaler.Unsignal();
                    }
                }
            }

            if (m_socketType.CanSend)
            {
                if (events.HasFlag(SocketEvents.Out))
                {
                    if (!m_flags.HasFlag(SocketFlags.Out))
                    {
                        m_flags |= SocketFlags.Out;
                        m_sendSignaler.Signal();
                    }
                }
                else
                {
                    if (m_flags.HasFlag(SocketFlags.Out))
                    {
                        m_flags &= ~SocketFlags.Out;
                        m_sendSignaler.Unsignal();
                    }
                }
            }
        }

        private OptionSet GetOptionSet(string name)
        {
            OptionSet optionSet;

            if (m_optionSets.TryGetValue(name,out optionSet))
                return optionSet;

            Transport.Transport transport = Global.GetTransport(name);

            if (transport == null)
                return null;

            optionSet = transport.GetOptionSet();

            if (optionSet == null)
                return null;

            m_optionSets.Add(name, optionSet);

            return optionSet;
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == StateMachine.ActionSourceId && type == StateMachine.StopAction)
            {
                Debug.Assert(m_state == SocketState.Active);

                // Close sndfd and rcvfd. This should make any current
                // select/poll using SNDFD and/or RCVFD exit.
                if (m_socketType.CanReceive)
                {
                    m_receiveSignaler.Dispose();
                    m_receiveSignaler = null;
                }

                if (m_socketType.CanSend)
                {
                    m_sendSignaler.Dispose();
                    m_sendSignaler = null;
                }

                foreach (Endpoint endpoint in m_endpoints)
                {
                    m_shutingdownEndpoints.Add(endpoint);
                    endpoint.Stop();
                }
                m_endpoints.Clear();

                m_state = SocketState.StoppingEndpoints;

                if (m_shutingdownEndpoints.Count == 0)
                {
                    m_state = SocketState.Stopping;
                    m_socketBase.Stop();
                }
            }
            else if (m_state == SocketState.StoppingEndpoints)
            {
                Debug.Assert(sourceId == Endpoint.SourceId && type == Endpoint.EndpointStoppedEvent);
                Endpoint endpoint = (Endpoint)source;
                m_shutingdownEndpoints.Remove(endpoint);
                endpoint.Dispose();

                if (m_shutingdownEndpoints.Count == 0)
                {
                    m_state = SocketState.Stopping;

                    // stop and check if stop completed synchronously
                    if (m_socketBase.Stop())
                    {
                        // the stop completed, we can destroy the socket

                        m_socketBase.Dispose();
                        m_state = SocketState.Init;

                        m_terminateEvent.Set();
                    }
                }
            }
            else if (m_state == SocketState.Stopping)
            {
                Debug.Assert(sourceId == StateMachine.ActionSourceId && type == StoppedAction);

                m_socketBase.Dispose();
                m_state = SocketState.Init;

                m_terminateEvent.Set();
            }
            else
            {
                // TODO: throw bad state
            }
        }

        protected override void Handle(int sourceId, int type, StateMachine source)
        {
            switch (m_state)
            {
                case SocketState.Init:
                    switch (sourceId)
                    {
                        case StateMachine.ActionSourceId:
                            switch (type)
                            {
                                case StateMachine.StartAction:
                                    m_state = SocketState.Active;
                                    break;
                                default:
                                    // TODO: throw bad action
                                    break;
                            }
                            break;

                        default:
                            // TODO: throw bad source
                            break;
                    }
                    break;
                case SocketState.Active:
                    switch (sourceId)
                    {                     
                        case Endpoint.SourceId:
                            switch (type)
                            {
                                case Endpoint.EndpointStoppedEvent:
                                    Endpoint endpoint = (Endpoint)source;
                                    m_shutingdownEndpoints.Remove(endpoint);
                                    endpoint.Dispose();
                                    break;
                                default:
                                    // TODO: throw bad action
                                    break;
                            }
                            break;
                        default:
                            // The assumption is that all the other events come from pipes
                            switch (type)
                            {
                                case PipeBase.InEvent:
                                    m_socketBase.In((PipeBase)source);
                                    break;
                                case PipeBase.OutEvent:
                                    m_socketBase.Out((PipeBase)source);
                                    break;
                                default:
                                    // TODO: throw bad action
                                    break;
                            }
                            break;
                    }
                    break;
                default:
                    // TODO: throw bad state
                    break;
            }
        }
    }
}
