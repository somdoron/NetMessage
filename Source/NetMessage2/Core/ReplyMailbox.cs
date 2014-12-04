using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage2.Core
{
    public class ReplyMailbox : IDisposable
    {
        public ActorAddress ActorAddress { get; private set; }

        public T WaitForReply<T>()
        {
            throw new NotImplementedException();   
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
