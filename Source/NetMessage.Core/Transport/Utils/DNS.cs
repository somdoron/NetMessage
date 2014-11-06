using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;

namespace NetMessage.Core.Transport.Utils
{
    class DNSResult
    {
        public bool Error { get; set; }
        public IPAddress Address { get; set; }
    }

    class DNS : StateMachine
    {
        public const int StoppedEvent = 1;
        public const int DoneEvent = 2;

        enum State
        {
            Idle = 1, Resolving, Done, Stopping
        }

        private const int DoneAction = 1;
        private const int CancelledAction = 2;

        private State m_state;
        private StateMachineEvent m_doneEvent;

        private DNSResult m_result;

        private bool m_isIPV4;

        public DNS(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();
        }

        public override void Dispose()
        {
            Debug.Assert(m_state == State.Idle);

            m_doneEvent.Dispose();
            base.Dispose();
        }

        public bool IsIdle
        {
            get
            {
                return IsStateMachineIdle;
            }
        }

        public void Start(string address, bool ip4Only, DNSResult result)
        {
            Debug.Assert(m_state == State.Idle);

            m_result = result;
            m_isIPV4 = ip4Only;

            IPAddress ipAddress;

            if (IPAddress.TryParse(address, out ipAddress))
            {
                if (ip4Only && ipAddress.AddressFamily != AddressFamily.InterNetwork)
                {
                    throw new ArgumentException("IPAddress is not IPV4", "address");
                }

                result.Error = false;
                result.Address = ipAddress;

                base.StartStateMachine();
                Action(DoneAction);
                return;
            }

            Dns.BeginGetHostAddresses(address, OnCompleted, null);

            StartStateMachine();
        }

        public void Stop()
        {
            StopStateMachine();
        }

        private void OnCompleted(IAsyncResult result)
        {
            IEnumerable<IPAddress> addresses = Dns.EndGetHostAddresses(result);

            Context.Enter();
            try
            {
                if (m_state == State.Stopping)
                {
                    Action(CancelledAction);
                }
                else
                {
                    if (m_isIPV4)
                    {
                        addresses = addresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                    }
                    else
                    {
                        addresses =
                            addresses.Where(
                                ip =>
                                    ip.AddressFamily == AddressFamily.InterNetwork ||
                                    ip.AddressFamily == AddressFamily.InterNetworkV6);
                    }

                    if (!addresses.Any())
                    {
                        m_result.Error = true;
                        m_result.Address = null;
                        Action(DoneAction);
                    }
                    else
                    {
                        m_result.Error = false;
                        m_result.Address = addresses.First();
                    }
                }
            }
            finally
            {
                Context.Leave();
            }
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == ActionSourceId && type == StopAction)
            {
                if (m_state == State.Resolving)
                {
                    // in nanomsg the async call was cancelled, this feature doesn't exist on windows, we just wait until the call complete                    
                    m_state = State.Stopping;
                }
                else
                {
                    Stopped(StoppedEvent);
                    m_state = State.Idle;
                }
            }
            else if (m_state == State.Stopping)
            {
                if (sourceId == ActionSourceId && (type == CancelledAction || type == DoneAction))
                {
                    Stopped(StoppedEvent);
                    m_state = State.Idle;
                }
            }
        }

        protected override void Handle(int sourceId, int type, StateMachine source)
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
                                    m_state = State.Resolving;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Resolving:
                    switch (sourceId)
                    {
                        case DoneAction:
                            Raise(m_doneEvent, DoneEvent);
                            break;
                    }
                    break;
            }
        }
    }
}
