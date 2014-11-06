using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.AsyncIO;
using NetMessage.Transport;
using NetMessage.Transport.InProc;
using NetMessage.Transport.Tcp;

namespace NetMessage
{
    public static class Global
    {
        private static Pool s_pool;

        private static Dictionary<string, TransportBase> s_transports;

        static Global()
        {
            s_pool = new Pool();

            s_transports = new Dictionary<string, TransportBase>();

            RegisterTransport(new InProcTransport());
            RegisterTransport(new TcpTransport());
        }

        internal static Pool Pool
        {
            get { return s_pool; }
        }

        internal static void RegisterTransport(TransportBase transport)
        {
            lock (s_transports)
            {
                s_transports.Add(transport.Name, transport);
            }            
        }

        internal static TransportBase GetTransport(string name)
        {
            lock (s_transports)
            {
                TransportBase transport;

                s_transports.TryGetValue(name, out transport);

                return transport;
            }
        }
    }
}
