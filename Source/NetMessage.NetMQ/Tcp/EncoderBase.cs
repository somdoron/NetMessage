using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;

namespace NetMessage.NetMQ.Tcp
{
    public abstract class EncoderBase : StateMachine
    {
        public const int MessageSentEvent = 1;        
        public const int ErrorEvent = 3;

        protected const int BufferSentAction =1;

        protected EncoderBase(int sourceId, StateMachine owner)
            : base(sourceId, owner)
        {
        }

        public abstract bool NoDelay
        {
            get; set; }

        public void Sent()
        {
            Action(BufferSentAction);
        }

        public void SwapOwner(ref StateMachine owner, ref int ownerSourceId)
        {
            SwapStateMachineOwner(ref owner, ref ownerSourceId);
        }
        
        public abstract void Start(USocket socket);

        public abstract void Send(NetMQMessage message);

    }
}
