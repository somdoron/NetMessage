using System.Threading;

namespace NetMessage.Core.Utils
{
    public class Signaler
    {
        private ManualResetEventSlim m_manualResetEvent = new ManualResetEventSlim(false);        

        internal bool Wait(int timeout)
        {
            return m_manualResetEvent.Wait(timeout);
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
