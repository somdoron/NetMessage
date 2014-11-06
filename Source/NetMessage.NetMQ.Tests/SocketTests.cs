using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetMessage.Tests
{
    [TestFixture]
    public class SocketTests
    {
        [Test]
        public void OneMessage([NUnit.Framework.Values("tcp://127.0.0.1:5555")] string address)
        {
            using (Socket server = Socket.CreateRouter())
            {
                server.Bind(address);

                using (Socket client = Socket.CreateDealer())
                {
                    client.Connect(address);

                    Message message = new Message();
                    message.Append("hello");

                    client.SendMessage(message);

                    message = server.ReceiveMessage();

                    Assert.AreEqual(message[1].ConvertToString() ,"hello");
                    Assert.AreEqual(message.FrameCount, 2);
                }
            }
        }
    }
}
