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
using NetMessage.NetMQ;

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
            client.Connect("localhost", 6666);

            Socket server = lisenter.Accept();

            Task.Factory.StartNew(() =>
            {
                byte[] buffer = new byte[1000];

                while (true)
                {
                    server.Receive(buffer);
                }
            });

            byte[] data = new byte[100];


            int count = 100000;

            int index = 0;

            Stopwatch stopwatch = new Stopwatch();
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            
            SocketAsyncEventArgs socketAsyncEventArgs = new SocketAsyncEventArgs();            
            socketAsyncEventArgs.Completed += delegate(object sender, SocketAsyncEventArgs eventArgs)
            {
                index++;

                if (index == count)
                {
                    stopwatch.Stop();
                    manualResetEvent.Set();
                }

                socketAsyncEventArgs.SetBuffer(data, 0, data.Length);
                client.SendAsync(socketAsyncEventArgs);
            };

            stopwatch.Start();

            socketAsyncEventArgs.SetBuffer(data, 0, data.Length);
            client.SendAsync(socketAsyncEventArgs);

            manualResetEvent.WaitOne();

            Console.WriteLine("{0:N0} per second", (double)count / stopwatch.ElapsedMilliseconds * 1000);
            Console.ReadLine();
        }
    }
}
