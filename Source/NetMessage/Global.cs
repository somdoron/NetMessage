using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Transport.InProc;

namespace NetMessage
{
    public static class Global
    {
        private static Dictionary<string, Transport.Transport> s_transports;


        static Global()
        {
            s_transports = new Dictionary<string, Transport.Transport>();

            RegisterTransport(new InProcTransport());
        }

        public static void RegisterTransport(Transport.Transport transport)
        {
            lock (s_transports)
            {
                s_transports.Add(transport.Name, transport);    
            }            
        }

        public static Transport.Transport GetTransport(string name)
        {
            lock (s_transports)
            {
                Transport.Transport transport;

                s_transports.TryGetValue(name, out transport);

                return transport;
            }
        }
    }
}
