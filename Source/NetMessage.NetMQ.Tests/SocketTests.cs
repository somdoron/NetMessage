using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMessage.NetMQ.Tests
{
    [TestFixture]
    public class SocketTests
    {
        [Test]
        public void OneMessage([NUnit.Framework.Values("tcp://127.0.0.1:5555")] string address)
        {
            using (NetMQSocket server = NetMQSocket.CreateDealer())
            {
                server.Bind(address);

                using (NetMQSocket client = NetMQSocket.CreateDealer())
                {
                    client.Connect(address);

                    client.Send("Hello");

                    string message = server.ReceiveString();

                    Assert.AreEqual(message,"Hello");
                    Assert.IsFalse(server.IsMore);
                }
            }
        }
    }
}
