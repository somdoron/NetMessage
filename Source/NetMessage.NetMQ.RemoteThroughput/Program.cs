using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            using (NetMQSocket dealerSocket = NetMQSocket.CreateDealer())
            {
                dealerSocket.Connect(connectTo);

                var message = new NetMQMessage(1);
                message.Append(new byte[messageSize]);
                dealerSocket.SendMessage(message);

                Stopwatch stopwatch = Stopwatch.StartNew();

                for (int i = 0; i != messageCount-1; i++)
                {
                    message = new NetMQMessage(1);
                    message.Append(new byte[messageSize]);
                    dealerSocket.SendMessage(message);
                }

                stopwatch.Stop();

                double messagesPerSecond = (double)messageCount / stopwatch.ElapsedMilliseconds * 1000;
                
                double megabits = messagesPerSecond * messageSize * 8 / 1000000;

                Console.WriteLine("message size: {0} [B]", messageSize);
                Console.WriteLine("message count: {0}", messageCount);
                Console.WriteLine("mean throughput: {0:0.000} [msg/s]", messagesPerSecond);
                Console.WriteLine("mean throughput: {0:0.000} [Mb/s]", megabits);

                Console.ReadLine();
            }
            
            return 0;
        }
    }
}
