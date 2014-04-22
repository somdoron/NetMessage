using System;

namespace NetMessage.Core.Transport
{
    public abstract class Transport<T> : IDisposable where T: MessageBase
    {
        public abstract OptionSet GetOptionSet();

        public abstract string Name { get; }        

        public abstract void Dispose();
        
        public abstract EndpointBase<T> Bind(object hint);

        public abstract EndpointBase<T> Connect(object hint);
    }
}
