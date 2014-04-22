using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.Patterns;

namespace NetMessage.Core
{
    public static class SocketFactory
    {
        public static Socket CreateDealer()
        {
            return new Socket(new Dealer.DealerSocketType());
        }

        public static Socket CreateRouter()
        {
            return new Socket(new Router.RouterSocketType());
        }        
    }    
}
