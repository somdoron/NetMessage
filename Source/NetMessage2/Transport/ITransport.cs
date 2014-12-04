using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using NetMessage2.Core;

namespace NetMessage2.Transport
{
    public interface ITransport
    {
        bool IsSocketTypeSupported(SocketType socketType);

        Func<Actor, ActorAddress> ListenerFactory { get; }
        Func<Actor, ActorAddress> ConnectorFactory { get; } 
        string Name { get; }
    }
}
