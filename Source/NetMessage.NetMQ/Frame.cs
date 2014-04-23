using System;
using System.Collections.Generic;
using System.Text;

namespace NetMessage.NetMQ
{
    public class NetMQFrame : IEquatable<NetMQFrame>, IEnumerable<byte>
    {
        private int m_messageSize;

        public NetMQFrame()
        {
            m_messageSize = 0;
            Buffer = new ArraySegment<byte>(new byte[0]);
        }

        public NetMQFrame(int size)
        {
            m_messageSize = size;
            Buffer = new ArraySegment<byte>(new byte[size]);
        }

        public NetMQFrame(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            Buffer = new ArraySegment<byte>(buffer);
            m_messageSize = buffer.Length;
        }

        public NetMQFrame(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            Buffer = new ArraySegment<byte>(buffer, offset, count);
            m_messageSize = count;
        }

        public NetMQFrame(string message)
            : this(Encoding.ASCII.GetBytes(message))
        {

        }

        public ArraySegment<byte> Buffer { get; private set; }

        /// <summary>
        /// Gets or sets the size of the message data contained in the frame.
        /// </summary>
        public int MessageSize
        {
            get
            {
                return m_messageSize;
            }
            set
            {
                if (value < 0 || value > BufferSize)
                {
                    throw new ArgumentOutOfRangeException("value", "Expected non-negative value less than or equal to the buffer size.");
                }

                m_messageSize = value;
            }
        }

        /// <summary>
        /// Gets the maximum size of the frame buffer.
        /// </summary>
        public int BufferSize
        {
            get { return Buffer.Count; }
        }

        /// <summary>
        /// Gets an empty <see cref="NetMQFrame"/> that may be used as message separators.
        /// </summary>
        public static NetMQFrame Empty
        {
            get { return new NetMQFrame(0); }
        }

        public byte this[int index]
        {
            get
            {
                if (index < 0 || index > MessageSize)
                {
                    throw new ArgumentOutOfRangeException("index", "Expected non-negative value less than or equal to the message size.");
                }

                return Buffer.Array[Buffer.Offset + index];
            }
            set
            {
                if (index < 0 || index > MessageSize)
                {
                    throw new ArgumentOutOfRangeException("index", "Expected non-negative value less than or equal to the message size.");
                }

                Buffer.Array[Buffer.Offset + index] = value;
            }
        }

        /// <summary>
        /// Create a copy of the supplied buffer and store it in a <see cref="NetMQFrame"/>.
        /// </summary>
        /// <param name="buffer">The <see cref="byte"/> array to copy.</param>
        /// <returns>A <see cref="NetMQFrame"/> containing a copy of <paramref name="buffer"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
        public static NetMQFrame Copy(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            var copy = new NetMQFrame(buffer.Length);

            System.Buffer.BlockCopy(buffer, 0, copy.Buffer.Array, copy.Buffer.Offset, buffer.Length);

            return copy;
        }

        /// <summary>
        /// Create a copy of the supplied <see cref="NetMQFrame"/>.
        /// </summary>
        /// <param name="frame">The <see cref="NetMQFrame"/> to copy.</param>
        /// <returns>A <see cref="NetMQFrame"/> containing a copy of <paramref name="frame"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="frame"/> is null.</exception>
        public static NetMQFrame Copy(NetMQFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            var copy = new NetMQFrame(new byte[frame.BufferSize]);
            copy.MessageSize = frame.MessageSize;

            System.Buffer.BlockCopy(frame.Buffer.Array, frame.Buffer.Offset, copy.Buffer.Array, copy.Buffer.Offset, frame.BufferSize);

            return copy;
        }

        public string ConvertToString()
        {
            return Encoding.ASCII.GetString(Buffer.Array, Buffer.Offset, this.MessageSize);
        }

        /// <summary>
        /// Determines whether the specified <see cref="NetMQFrame"/> is equal to the current <see cref="NetMQFrame"/>.
        /// </summary>
        /// <param name="other">The <see cref="NetMQFrame"/> to compare with the current <see cref="NetMQFrame"/>.</param>
        /// <returns>true if the specified System.Object is equal to the current System.Object; otherwise, false.</returns>
        public bool Equals(NetMQFrame other)
        {
            if (MessageSize > other.BufferSize || MessageSize != other.MessageSize)
            {
                return false;
            }

            for (int i = 0; i < MessageSize; i++)
            {
                if (this[i] != other[i])
                {
                    return false;
                }
            }

            return true;
        }

        public byte[] ToByteArray(bool copy = false)
        {
            if (!copy && MessageSize == BufferSize && Buffer.Offset == 0)
            {
                return Buffer.Array;
            }
            else
            {
                byte[] byteArray = new byte[MessageSize];

                System.Buffer.BlockCopy(Buffer.Array, Buffer.Offset, byteArray, 0, MessageSize);

                return byteArray;
            }
        }


        public IEnumerator<byte> GetEnumerator()
        {
            for (int i = 0; i < MessageSize; i++)
            {
                yield return this[i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < MessageSize; i++)
            {
                yield return this[i];
            }
        }
    }
}