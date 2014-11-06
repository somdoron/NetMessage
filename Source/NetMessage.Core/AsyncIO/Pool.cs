using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.AsyncIO
{
    class Pool : IDisposable
    {
        private Worker m_worker;

        public Pool()
        {
            m_worker = new Worker();
        }

        public void Dispose()
        {
            m_worker.Dispose();
        }

        public Worker ChooseWorker()
        {
            return m_worker;
        }        
    }
}
