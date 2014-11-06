using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.AsyncIO;

namespace NetMessage.Core.Transport.Utils
{
    class Backoff : IDisposable
    {
        public const int TimeOutEvent = Timer.TimeOutEvent;
        public const int StoppedEvent = Timer.StoppedEvent;

        private Timer m_timer;
        private int m_multiplier;

        public Backoff(int sourceId, int minimumInterval, int maximumInterval, StateMachine owner)
        {
            MinimumInterval = minimumInterval;
            MaximumInterval = maximumInterval;
            m_timer = new Timer(sourceId, owner);

            m_multiplier = 1;
        }

        public int MinimumInterval { get; private set; }
        public int MaximumInterval { get; private set; }

        public bool IsIdle
        {
            get
            {
                return m_timer.IsIdle;
            }
        }

        public void Dispose()
        {
            m_timer.Dispose();
        }

        public void Start()
        {
            int timeout;

            timeout = (m_multiplier - 1) * MinimumInterval;

            if (timeout > MaximumInterval)
            {
                timeout = MaximumInterval;
            }
            else
            {
                m_multiplier *= 2;
            }

            m_timer.Start(timeout);
        }

        public void Stop()
        {
            m_timer.Stop();
        }

        public void Reset()
        {
            m_multiplier = 1;
        }
    }
}
