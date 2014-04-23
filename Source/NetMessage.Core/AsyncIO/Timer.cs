using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OSTimer = System.Threading.Timer;

namespace NetMessage.Core.AsyncIO
{
    public class Timer : StateMachine
    {
        public const int TimeOutEvent = 1;
        public const int StoppedEvent = 2;

        enum State
        {
            Idle = 1, Active
        }

        private State m_state;

        private const int TimeOutAction = 1;

        private StateMachineEvent m_doneEvent;

        private OSTimer m_osTimer;

        public Timer(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
            m_osTimer = new OSTimer(OnTimeout, null, Timeout.Infinite, Timeout.Infinite);
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();
        }

        public override void Dispose()
        {
            m_doneEvent.Dispose();
            m_osTimer.Dispose();
        }

        public bool IsIdle
        {
            get
            {
                return IsStateMachineIdle;
            }
        }

        public void Start(int timeout)
        {
            m_osTimer.Change(timeout, Timeout.Infinite);
            StartStateMachine();
        }

        public void Stop()
        {
            StopStateMachine();
        }

        private void OnTimeout(object state)
        {
            Context.Enter();
            try
            {
                if (m_state == State.Active)
                    Action(TimeOutAction);
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
                m_osTimer.Change(Timeout.Infinite, Timeout.Infinite);

                m_state = State.Idle;
                Stopped(StoppedEvent);
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
                                    m_state = State.Active;
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Active:
                    switch (sourceId)
                    {
                        case ActionSourceId:
                            switch (type)
                            {
                                case TimeOutAction:
                                    Raise(m_doneEvent, TimeOutEvent);
                                    break;
                            }

                            break;
                    }
                    break;
            }
        }
    }
}
