using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Core
{
    public class Message : IEnumerable<Frame>
    {
        private List<Frame> m_frames;

        public Message(int frames)
        {
            m_frames = new List<Frame>(frames);
        }

        public Message()
        {
            m_frames = new List<Frame>();
        }

        public Message(IEnumerable<Frame> frames)
        {
            if (frames == null)
            {
                throw new ArgumentNullException("frames");
            }

            m_frames = new List<Frame>(frames);
        }

        public Message(byte[] buffers)
        {
            if (buffers == null)
            {
                throw new ArgumentNullException("buffers");
            }

            m_frames = new List<Frame>(buffers.Select(b => new Frame(b)));
        }

        /// <summary>
        /// Gets the <see cref="Frame"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the <see cref="Frame"/> to get.</param>
        /// <returns>The <see cref="Frame"/> at the specified index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/>is less than 0 -or- <paramref name="index"/> is equal to or greater than <see cref="FrameCount"/>.
        /// </exception>
        public Frame this[int index]
        {
            get
            {
                return m_frames[index];
            }
        }

        /// <summary>
        /// Gets the number of <see cref="NetMQFrame"/> objects contained by this message.
        /// </summary>
        public int FrameCount
        {

            get
            {
                return m_frames.Count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current message is empty.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return FrameCount == 0;
            }
        }
        /// <summary>
        /// Gets the first frame in the current message.
        /// </summary>
        public Frame First
        {
            get { return m_frames[0]; }
        }

        /// <summary>
        /// Gets the last frame in the current message.
        /// </summary>
        public Frame Last
        {
            get { return m_frames[m_frames.Count - 1]; }
        }


        public IEnumerator<Frame> GetEnumerator()
        {
            return m_frames.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return m_frames.GetEnumerator();
        }

        public void Append(byte[] buffer)
        {
            Frame frame = new Frame(buffer);
            m_frames.Add(frame);
        }

        public void Append(Frame frame)
        {
            m_frames.Add(frame);
        }

        public void Append(string message)
        {
            m_frames.Add(new Frame(message));
        }

        public void AppendEmptyFrame()
        {
            m_frames.Add(Frame.Empty);
        }

        public void Push(Frame frame)
        {
            m_frames.Insert(0, frame);
        }

        public void Push(byte[] bytes)
        {
            Frame frame = new Frame(bytes);
            m_frames.Insert(0, frame);
        }

        public void Push(string message)
        {
            m_frames.Insert(0, new Frame(message));
        }

        public void PushEmptyFrame()
        {
            m_frames.Insert(0, Frame.Empty);
        }

        /// <summary>
        /// Removes the first frame from a message
        /// </summary>
        /// <returns></returns>
        public Frame Pop()
        {
            Frame frame = m_frames[0];
            m_frames.RemoveAt(0);

            return frame;
        }

        public void RemoveFrame(Frame frame)
        {
            m_frames.Remove(frame);
        }

        public void Clear()
        {
            m_frames.Clear();
        }

        /// <summary>
        /// Returns a string showing the frame contents.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (m_frames.Count == 0)
                return "Message[<no frames>]";
            StringBuilder sb = new StringBuilder("Message[");
            bool first = true;
            foreach (Frame f in m_frames)
            {
                if (!first)
                    sb.Append(",");

                // TODO: convert to HEX or Base something instead of string
                sb.Append(f.ConvertToString());
                first = false;
            }
            return sb.Append("]").ToString();
        }
    }
}
