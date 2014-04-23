using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;

namespace NetMessage.NetMQ.Tcp
{
    public abstract class DecoderBase : StateMachine
    {
        public const int MessageReceivedEvent = 1;
        public const int StoppedEvent = 2;
        public const int ErrorEvent = 3;

        protected const int ReceivedType = 1;


        protected DecoderBase(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
        }

        
        public void Received()
        {
            Action(ReceivedType);            
        }       

        public abstract bool IsIdle { get; }

        public abstract void Start(USocket socket, NetMQMessage message);

        public abstract void Stop();
    }
}
