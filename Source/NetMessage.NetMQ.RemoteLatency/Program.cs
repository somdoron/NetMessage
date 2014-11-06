using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.NetMQ.RemoteLatency
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("usage: remote_lat remote_lat <connect-to> <message-size> <roundtrip-count>");
                return 1;
            }

            string connectTo = args[0];
            int messageSize = int.Parse(args[1]);
            int roundtripCount = int.Parse(args[2]);

            using (var reqSocket = Socket.CreateDealer())
            {
                reqSocket.Connect(connectTo);

                var message = new Message(1);
                message.Append(new byte[messageSize]);

                var stopWatch = Stopwatch.StartNew();

                for (int i = 0; i != roundtripCount; i++)
                {
                    reqSocket.SendMessage(message);

                    message = reqSocket.ReceiveMessage();
                    if (message.FrameCount != 1 || message.First.MessageSize != messageSize)
                    {
                        Console.WriteLine("message of incorrect size received. Received: " + message.First.MessageSize +
                                          " Expected: " + messageSize);
                        return -1;
                    }
                }

                stopWatch.Stop();                

                double elapsedMicroseconds = stopWatch.ElapsedTicks*1000000/Stopwatch.Frequency;
                double latency = elapsedMicroseconds/(roundtripCount*2);

                Console.WriteLine("message size: {0} [B]", messageSize);
                Console.WriteLine("roundtrip count: {0}", roundtripCount);
                Console.WriteLine("average latency: {0:0.000} [µs]", latency);
                Console.ReadLine();
            }            

            return 0;
        }
    }
}
