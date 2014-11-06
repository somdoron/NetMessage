using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.AsyncIO;

namespace NetMessage.AsyncIO
{
    class Timerset
    {
        public void Add(int timeout, Timer timer)
        {
    
        }

        public void Remove(Timer timer)
        {
            
        }

        public bool TryGetEvent(out Timer timer)
        {
            timer = null;
            return false;
        }

        public int Timeout()
        {
            return -1;
        }
    }
}
