namespace NetMessage.NetMQ
{
    public static class SocketTypes
    {
        public const int Pair = 0x00;
        public const int Publisher = 0x01;
        public const int Subscriber = 0x02;
        public const int Request = 0x03;
        public const int Response = 0x04;
        public const int Dealer = 0x05;
        public const int Router = 0x06;
        public const int Pull = 0x07;
        public const int Push = 0x08;
    }
}
