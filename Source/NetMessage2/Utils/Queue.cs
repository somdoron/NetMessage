using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetMessage2.Utils
{
    /// <summary>
    /// Concurrent queue
    /// </summary>
    public class Queue<T>
    {
        private ConcurrentQueue<T> m_queue;

        private long m_writeIndex;
        private long m_readIndex;

        private long m_lastAllowedToReadIndex;

        public Queue()
        {
            m_queue = new ConcurrentQueue<T>();
            m_writeIndex = 0;
            m_readIndex = 0;
            m_lastAllowedToReadIndex = 0;
        }

        public bool CanRead
        {
            get
            {
                
            }
        }

        /// <summary>
        /// Insert item to the queue
        /// </summary>
        /// <param name="item"></param>
        /// <returns>False if the reader thread is asleep</returns>
        public bool Enqueue(ref T item)
        {
            m_queue.Enqueue(item);

            if (Interlocked.CompareExchange(ref m_lastAllowedToReadIndex, m_writeIndex + 1, m_writeIndex) !=
                m_writeIndex)
            {
                // Interlocked was unsuccessful because the reader is asleep, we can update the index without interlocked
                m_writeIndex++;
                m_lastAllowedToReadIndex = m_writeIndex;

                return false;
            }

            m_writeIndex++;
            return true;
        }

        public bool Dequeue(ref T item)
        {                        
            // we read all items
            // TODO: we can improve performance by saving the last allowed from last time we fetch it
            if (m_readIndex == Interlocked.Read(ref m_lastAllowedToReadIndex))
            {
                // change to thread asleep
                if (Interlocked.CompareExchange(ref m_lastAllowedToReadIndex, -1, m_readIndex) == m_readIndex)
                {
                    return false;
                }
            }

            bool result = m_queue.TryDequeue(out item);
            Debug.Assert(result);

            if (result)
            {
                m_readIndex++;
            }
            
            return result;
        }

        public T Peek()
        {
            throw new NotImplementedException();
        }
    }
}
