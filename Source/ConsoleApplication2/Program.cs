using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ;

namespace ConsoleApplication2
{
    class Program
    {
        static void Main(string[] args)
        {
            using (NetMQContext context = NetMQContext.Create())
            {
                using (NetMQSocket client = context.CreateDealerSocket())
                {
                    client.Options.Identity = new[] { (byte)(byte)1, (byte)1, (byte)1 };
                    client.Connect("tcp://127.0.0.1:5555");                    

                    Console.WriteLine("Connected");

                    client.Send("hello");

                    Console.WriteLine("Sent... waiting...");

                    string reply = client.ReceiveString();

                    Console.WriteLine(reply);

                    Console.ReadLine();
                }
            }
        }
    }
}
