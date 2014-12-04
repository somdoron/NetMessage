using System;

namespace NetMessage2.Transport
{
    public class Connector
    {
        public class ConnectMessage
        {
            public Uri Uri { get; private set; }

            public ConnectMessage(Uri uri)
            {
                Uri = uri;
            }
        }

        public class ConnectReplyMessage
        {
            public ConnectReplyMessage(bool success)
            {
                Success = success;
            }

            public bool Success { get; private set; }
        }
    }
}