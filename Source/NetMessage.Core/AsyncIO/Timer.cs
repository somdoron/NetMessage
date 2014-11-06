using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetMessage.AsyncIO
{
    class Timer : StateMachine
    {
        public const int TimeOutEvent = 1;
        public const int StoppedEvent = 2;

        public const int StartTaskSourceId = 1;
        public const int StopTaskSourceId = 2;

        public const int TimeOutAction = 1;

        enum State
        {
            Idle = 1, Active, Stopping
        }

        private State m_state;
        private WorkerTask m_startTask;
        private WorkerTask m_stopTask;
        private Worker m_worker;
        private int m_timeout;

        private StateMachineEvent m_doneEvent;

        public Timer(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
            m_state = State.Idle;
            m_doneEvent = new StateMachineEvent();
            m_worker = ChooseWorker();
            m_timeout = -1;

            m_startTask = new WorkerTask(StartTaskSourceId, this);
            m_stopTask = new WorkerTask(StopTaskSourceId, this);
        }

        public override void Dispose()
        {
            m_doneEvent.Dispose();
            m_stopTask.Dispose();
            m_startTask.Dispose();

            base.Dispose();
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
            m_timeout = timeout;
            base.StartStateMachine();
        }

        public void Stop()
        {
            base.StopStateMachine();
        }

        internal override void Shutdown(int sourceId, int type, StateMachine source)
        {
            if (sourceId == ActionSourceId && type == StopAction)
            {
                m_state = State.Stopping;
                m_worker.Execute(m_stopTask);
                
            }
            else if (m_state == State.Stopping)
            {
                if (sourceId == StopTaskSourceId)
                {
                    m_worker.RemoveTimer(this);
                    m_state = State.Idle;
                    Stopped(StoppedEvent);
                }

                return;
            }
            else
            {
                Debug.Assert(false);    
            }            
        }

        internal override void Handle(int sourceId, int type, StateMachine source)
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
                                    m_worker.Execute(m_startTask);
                                    break;
                            }
                            break;
                    }
                    break;
                case State.Active:
                    switch (sourceId)
                    {
                        case StartTaskSourceId:
                            m_worker.AddTimer(m_timeout, this);
                            m_timeout = -1;
                            break;
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
