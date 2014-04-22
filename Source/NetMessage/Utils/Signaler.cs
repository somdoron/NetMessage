using System.Threading;

namespace NetMessage.Core.Utils
{
    public class Signaler
    {
        private ManualResetEvent m_manualResetEvent = new ManualResetEvent(false);        

        internal bool Wait(int timeout)
        {
            return m_manualResetEvent.WaitOne(timeout);
        }

        internal void Signal()
        {
            m_manualResetEvent.Set();
        }

        internal void Unsignal()
        {
            m_manualResetEvent.Reset();
        }

        internal void Dispose()
        {
            m_manualResetEvent.Reset();
        }
    }
}
