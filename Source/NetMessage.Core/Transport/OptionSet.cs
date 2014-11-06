using System;

namespace NetMessage.Transport
{
    public abstract class OptionSet : IDisposable
    {
        public abstract void SetOption(int option, object value);

        public abstract object GetOption(int option);

        public abstract void Dispose();
    }
}
