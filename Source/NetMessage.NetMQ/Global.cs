using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.Transport;
using NetMessage.NetMQ.InProc;
using NetMessage.NetMQ.Tcp;

namespace NetMessage.NetMQ
{
    public static class Global
    {
        private static Dictionary<string, Transport<NetMQMessage>> s_transports;

        static Global()
        {
            s_transports = new Dictionary<string, Transport<NetMQMessage>>();

            RegisterTransport(new InProcTransport());
            RegisterTransport(new TcpTransport());
        }

        public static void RegisterTransport(Transport<NetMQMessage> transport)
        {
            lock (s_transports)
            {
                s_transports.Add(transport.Name, transport);    
            }            
        }

        public static Transport<NetMQMessage> GetTransport(string name)
        {
            lock (s_transports)
            {
                Transport<NetMQMessage> transport;

                s_transports.TryGetValue(name, out transport);

                return transport;
            }
        }
    }
}
