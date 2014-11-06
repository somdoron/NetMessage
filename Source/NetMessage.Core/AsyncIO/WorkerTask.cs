using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.AsyncIO
{
    class WorkerTask : StateMachine
    {
        public const int ExecuteEvent = 1;

        public WorkerTask(int sourceId, StateMachine owner) : base(sourceId, owner)
        {
            SourceId = sourceId;
            Owner = owner;
        }

        public int SourceId { get; private set; }
        public StateMachine Owner { get; private set; }

        internal override void Handle(int sourceId, int type, StateMachine source)
        {
            
        }

        internal override void Shutdown(int sourceId, int type, StateMachine source)
        {
            
        }
    }
}
