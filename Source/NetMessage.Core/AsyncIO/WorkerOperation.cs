using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Core.AsyncIO
{
    class WorkerOperation : StateMachine
    {        
        public const int DoneEvent = 1;
        public const int ErrorEvent = 2;

        enum State { Idle, Acitve }

        private State m_state;

        public WorkerOperation(int sourceId, StateMachine owner) : base(sourceId, owner)
        {
            SourceId = sourceId;
            m_state = State.Idle;
        }

        public int SourceId { get; private set; }

        public bool IsIdle
        {
            get { return m_state == State.Idle; }
        }

        public void Start()
        {
            m_state = State.Acitve;
        }

        public void Stop()
        {
            m_state = State.Idle;
        }
        
        protected override void Handle(int sourceId, int type, StateMachine source)
        {
            
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            throw new NotImplementedException();
        }
    }
}
