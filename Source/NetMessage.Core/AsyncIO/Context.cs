using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetMessage.Core.AsyncIO
{
    delegate void OnContextLeaveDelegate(Context context);

    class Context : IDisposable
    {
        private readonly Pool m_pool;
        private OnContextLeaveDelegate m_onLeave;

        private Queue<StateMachineEvent> m_events;
        private Queue<StateMachineEvent> m_eventsTo;

        private object m_sync;
        
        public Context(Pool pool)
        {
            m_pool = pool;
            m_events = new Queue<StateMachineEvent>();
            m_eventsTo = new Queue<StateMachineEvent>();

            m_sync = new object();
        }

        public void SetOnLeave(OnContextLeaveDelegate onLeave)
        {
            m_onLeave = onLeave;
        }

        public void Dispose()
        {

        }

        public void Enter()
        {            
            Monitor.Enter(m_sync);          
        }

        public void Leave()
        {
            while (m_events.Count > 0)
            {
                var @event = m_events.Dequeue();
                @event.Process();
            }

            if (m_onLeave != null)
            {
                m_onLeave(this);
            }

            Queue<StateMachineEvent> eventsTo = null;

            if (m_eventsTo.Count > 0)
            {
                eventsTo = m_eventsTo;
                m_eventsTo = new Queue<StateMachineEvent>();
            }

            Monitor.Exit(m_sync);
            
            if (eventsTo != null)
            {
                while (eventsTo.Count > 0)
                {
                    var @event = eventsTo.Dequeue();

                    @event.StateMachine.Context.Enter();
                    try
                    {
                        @event.Process();
                    }
                    finally
                    {
                        @event.StateMachine.Context.Leave();
                    }
                }
            }
        }

        public void Raise(StateMachineEvent @event)
        {
            @event.Activate();
            m_events.Enqueue(@event);
        }

        public void RaiseTo(StateMachineEvent @event)
        {
            @event.Activate();
            m_eventsTo.Enqueue(@event);
        }

        public Worker ChooseWorker()
        {
            return m_pool.ChooseWorker();
        }
    }
}
