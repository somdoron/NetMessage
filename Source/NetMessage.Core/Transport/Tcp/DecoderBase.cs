using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;

namespace NetMessage.NetMQ.Tcp
{
    abstract class DecoderBase : StateMachine
    {                
        public const int ErrorEvent = 3;

        protected const int USocketReceivedAction = 1;


        protected DecoderBase(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
        }

        
        public void Received()
        {
            Action(USocketReceivedAction);            
        }


        public bool IsIdle
        {
            get { return IsStateMachineIdle; }
        }

        public abstract void Start(USocket socket);

        public abstract void Receive(out Message message);

        public void Stop()
        {
            StopStateMachine();
        }
    }
}
