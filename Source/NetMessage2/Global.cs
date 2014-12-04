using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage2.Core;
using NetMessage2.Transport;

namespace NetMessage2
{
    public static class Global
    {
        static internal Worker GetWorker()
        {
            throw new NotImplementedException();   
        }

        static internal ITransport GetTransport(string scheme)
        {
            throw new NotImplementedException();   
        }
    }
}
