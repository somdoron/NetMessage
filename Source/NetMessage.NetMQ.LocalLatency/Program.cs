using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.NetMQ.LocalLatency
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: local_lat <bind-to> <message-size> <roundtrip-count>");
                return 1;
            }

            string bindTo = args[0];
            int messageSize = int.Parse(args[1]);
            int roundtripCount = int.Parse(args[2]);


            using (var routerSocket = SocketFactory.CreateRouter())
            {
                routerSocket.Bind(bindTo);

                for (int i = 0; i != roundtripCount; i++)
                {                   
                    var message = routerSocket.ReceiveMessage();
                    if (message.FrameCount != 2 || message[1].MessageSize != messageSize)
                    {
                        Console.WriteLine("message of incorrect size received. Received: " + message.First().MessageSize +
                                          " Expected: " + messageSize);
                        return -1;
                    }

                    routerSocket.SendMessage(message);
                }

                Console.WriteLine("Replayed all!");
                Console.ReadLine();
            }

            return 0;
        }
    }
}
