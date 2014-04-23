using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetMessage.NetMQ;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            using (NetMQSocket server = SocketFactory.CreateDealer())
            {
                server.Bind("tcp://*:5555");

                using (NetMQSocket client = SocketFactory.CreateDealer())
                {
                    client.Connect("tcp://localhost:5555");                    

                    NetMQMessage message = new NetMQMessage();

                    message.Append("Hello");

                    client.SendMessage(message);

                    Console.WriteLine("Message sent");

                    var rMessage = server.ReceiveMessage();

                    Console.WriteLine("Message received");

                    Console.WriteLine(rMessage[0].ConvertToString());
                    
                    Console.ReadLine();
                }
            }

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
