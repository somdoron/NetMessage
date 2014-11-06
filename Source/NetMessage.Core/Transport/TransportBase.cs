using System;

namespace NetMessage.Transport
{
    abstract class TransportBase : IDisposable
    {
        public abstract OptionSet GetOptionSet();

        public abstract string Name { get; }        

        public abstract void Dispose();
        
        public abstract EndpointBase Bind(object hint);

        public abstract EndpointBase Connect(object hint);
    }
}
