using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Transport.Tcp
{
    public class TcpOptionSet : OptionSet
    {
        public TcpOptionSet()
        {
            NoDelay = false;
        }

        public bool NoDelay
        {
            get; set;
        }

        public override void SetOption(int option, object value)
        {
            throw new NotImplementedException();
        }

        public override object GetOption(int option)
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
