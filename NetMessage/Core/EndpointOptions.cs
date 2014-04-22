using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Core
{
    public class EndpointOptions
    {
        public bool IP4Only;
        public int SendPriority { get; set; }
        public int ReceivePriority { get; set; }

        public EndpointOptions Clone()
        {
            return new EndpointOptions
            {
                IP4Only = IP4Only,
                SendPriority = SendPriority,
                ReceivePriority = ReceivePriority
            };
        }
    }
}
