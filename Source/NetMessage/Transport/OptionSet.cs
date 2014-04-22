using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Transport
{
    public abstract class OptionSet : IDisposable
    {
        public abstract void SetOption(int option, object value);

        public abstract object GetOption(int option);

        public abstract void Dispose();
    }
}
