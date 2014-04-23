using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.NetMQ.RemoteThroughput
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: remote_thr <connect-to> <message-size> <message-count>");
                return 1;
            }

            string connectTo = args[0];
            int messageSize = int.Parse(args[1]);
            int messageCount = int.Parse(args[2]);

            using (NetMQSocket dealerSocket = SocketFactory.CreateDealer())
            {
                dealerSocket.Connect(connectTo);

                for (int i = 0; i != messageCount; i++)
                {
                    var message = new NetMQMessage(1);
                    message.Append(new byte[messageSize]);
                    dealerSocket.SendMessage(message);
                }

                Console.ReadLine();
            }
            
            return 0;
        }
    }
}
