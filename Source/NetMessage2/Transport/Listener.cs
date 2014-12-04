using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage2.Core;

namespace NetMessage2.Transport
{
    public class Listener
    {
        public class BindMessage
        {
            public Uri Uri { get; private set; }

            public BindMessage(Uri uri)
            {
                Uri = uri;
            }
        }

        public class BindReplyMessage
        {
            public BindReplyMessage(bool success)
            {
                Success = success;
            }

            public bool Success { get; private set; }
        }
        
    }
}
