using NetMessage.Core;
using NetMessage.NetMQ.Patterns;

namespace NetMessage.NetMQ
{
    public static class SocketFactory
    {
        public static NetMQSocket CreateDealer()
        {
            return new NetMQSocket(new Dealer.DealerSocketType());
        }

        public static NetMQSocket CreateRouter()
        {
            return new NetMQSocket(new Router.RouterSocketType());
        }        
    }    
}
