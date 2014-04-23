using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Transport;

namespace NetMessage.NetMQ.Tcp
{
    public abstract class HandshakeBase : StateMachine
    {
        public const int DoneEvent = 1;
        public const int ErrorEvent = 2;
        public const int StoppedEvent = 3;

        protected HandshakeBase(int sourceId, StateMachine owner) : base(sourceId, owner)
        {
            
        }

        public abstract bool IsIdle { get; }

        public abstract DecoderBase CreateDecoder(int sourceId, StateMachine owner);
        public abstract EncoderBase CreateEncoder(int sourceId, StateMachine owner);

        public abstract void Start(USocket socket, PipeBase<NetMQMessage> pipe);

        public abstract void Stop();

    }
}
