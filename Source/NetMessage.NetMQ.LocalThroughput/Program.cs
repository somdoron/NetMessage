using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.NetMQ.LocalThroughput
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: local_thr <bind-to> <message-size> <message-count>");
                return 1;
            }

            string bindTo = args[0];
            int messageSize = int.Parse(args[1]);
            int messageCount = int.Parse(args[2]);

            using (NetMQSocket dealerSocket = SocketFactory.CreateDealer())
            {
                dealerSocket.Bind(bindTo);

                var message = dealerSocket.ReceiveMessage();

                Console.WriteLine("First message received...");

                var stopWatch = Stopwatch.StartNew();
                for (int i = 0; i != messageCount - 1; i++)
                {
                    message = dealerSocket.ReceiveMessage();
                    if (message[0].MessageSize != messageSize)
                    {
                        Console.WriteLine("message of incorrect size received. Received: " + message.First.MessageSize +
                                          " Expected: " + messageSize);
                        return -1;
                    }
                }
                stopWatch.Stop();
                var millisecondsElapsed = stopWatch.ElapsedMilliseconds;
                if (millisecondsElapsed == 0)
                    millisecondsElapsed = 1;                

                double messagesPerSecond = (double)messageCount / millisecondsElapsed * 1000;
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
