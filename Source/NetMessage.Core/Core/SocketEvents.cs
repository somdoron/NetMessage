using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Core.Core
{
    [Flags]
    public enum SocketEvents
    {
        None=0,
        In=1,
        Out=2,
    }
}
