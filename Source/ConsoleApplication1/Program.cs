using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            Socket lisenter = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            lisenter.Bind(new IPEndPoint(IPAddress.Any, 6666));
            lisenter.Listen(1);

            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            client.ReceiveBufferSize = 0;
            client.SendBufferSize = 0;
            client.NoDelay = true;
            client.Connect("localhost", 6666);

            Socket server = lisenter.Accept();
            server.ReceiveBufferSize = 0;
            server.SendBufferSize = 0;
            server.NoDelay = true;

            byte[] buffer = new byte[10];

            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < 10000; i++)
            {
                client.Send(buffer);
                Console.WriteLine("{0:G} {1}", stopwatch.ElapsedMilliseconds, i);
            }
           
            Console.WriteLine("Send completed in {0}ms", stopwatch.ElapsedMilliseconds);
        }
    }
}
