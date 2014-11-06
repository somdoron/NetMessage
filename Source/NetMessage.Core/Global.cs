using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;
using NetMessage.Core.Transport;

namespace NetMessage
{
    public static class Global
    {
        private static Pool s_pool;

        //private static Dictionary<string, Transport> s_transports;

        static Global()
        {
            s_pool = new Pool();

            //s_transports = new Dictionary<string, Transport>();

            //RegisterTransport(new InProcTransport());
            //RegisterTransport(new TcpTransport());
        }

        internal static Pool Pool
        {
            get { return s_pool; }
        }

        //public static void RegisterTransport(Transport transport)
        //{
        //    //lock (s_transports)
        //    //{
        //    //    s_transports.Add(transport.Name, transport);    
        //    //}            
        //}

        //public static Transport GetTransport(string name)
        //{
        //    //lock (s_transports)
        //    //{
        //    //    Transport transport;

        //    //    s_transports.TryGetValue(name, out transport);

        //    //    return transport;
        //    //}
        //}
    }
}
