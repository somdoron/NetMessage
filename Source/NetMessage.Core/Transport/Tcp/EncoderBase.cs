using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;

namespace NetMessage.NetMQ.Tcp
{
    abstract class EncoderBase : StateMachine
    {        
        public const int ErrorEvent = 1;

        protected const int USocketSentAction =1;

        protected EncoderBase(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
        }

        public void Sent()
        {
            Action(USocketSentAction);
        }

        public bool IsIdle
        {
            get { return IsStateMachineIdle; }
        }
        
        public abstract void Start(USocket socket);

        public abstract void Send(Message message);

        public virtual void Stop()
        {
            StopStateMachine();
        }

    }
}
