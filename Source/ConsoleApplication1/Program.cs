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

                while (true)
                {
                    NetMQMessage message = server.ReceiveMessage();

                    Console.WriteLine(message[0].ConvertToString());

                    NetMQMessage replyMessage = new NetMQMessage();
                    replyMessage.Append("Reply !!!!");

                    server.SendMessage(replyMessage);
                }
            }

        }
    }
}
