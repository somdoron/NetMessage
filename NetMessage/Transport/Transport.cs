using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core;

namespace NetMessage.Transport
{
    public abstract class Transport : IDisposable
    {
        public abstract OptionSet GetOptionSet();

        public abstract string Name { get; }        

        public abstract void Dispose();
        
        public abstract EndpointBase Bind(object hint);

        public abstract EndpointBase Connect(object hint);
    }
}
