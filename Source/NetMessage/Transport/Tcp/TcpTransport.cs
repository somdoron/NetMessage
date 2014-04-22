using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Transport.Tcp
{
    public class TcpTransport : Transport
    {
        public override OptionSet GetOptionSet()
        {
            return new TcpOptionSet();
        }

        public override string Name
        {
            get { return "tcp"; }
        }

        public override void Dispose()
        {
            
        }

        public override EndpointBase Bind(object hint)
        {
            
        }

        public override EndpointBase Connect(object hint)
        {
            throw new NotImplementedException();
        }
    }
}
