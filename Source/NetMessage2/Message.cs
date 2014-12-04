using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage2.Utils;

namespace NetMessage2
{
    public struct Message
    {
        #region Frame

        enum FrameType
        {
            Empty,
            Integer,
            Long,
            String,
            Guid,
            ByteArray,
        }

        struct Frame
        {
            public Frame(int n)
                : this()
            {
                Integer = n;
                Type = FrameType.Integer;
            }

            public Frame(long n)
                : this()
            {
                Long = n;
                Type = FrameType.Long;
            }

            public Frame(string s, Encoding encoding)
                : this()
            {
                String = s;
                Encoding = encoding;
                Type = FrameType.String;
            }

            public Frame(Guid guid)
                : this()
            {
                Guid = guid;
                Type = FrameType.Guid;
            }

            public Frame(byte[] b)
                : this()
            {
                ByteArray = b;
                Type = FrameType.ByteArray;
                Offset = 0;
                Size = b.Length;
            }

            public Frame(byte[] b, int offset, int count)
                : this()
            {
                ByteArray = b;
                Type = FrameType.ByteArray;
                Offset = offset;
                Size = count;
            }

            public FrameType Type;
            public int Integer;
            public long Long;
            public string String;
            public Encoding Encoding;
            public Guid Guid;
            public byte[] ByteArray;
            public int Offset;
            public int Size;
        }

        #endregion

        private List<Frame> m_frames;
        private bool m_initialized;

        public void Init()
        {
            m_initialized = true;
            m_frames = new List<Frame>();
        }

        public void Close()
        {
            CheckInitialized();

            m_frames = null;
            m_initialized = false;
        }

        private void CheckInitialized()
        {
            if (!m_initialized)
            {
                throw new ObjectDisposedException("Message");
            }
        }

        public int Count
        {
            get
            {
                CheckInitialized(); 
                
                return m_frames.Count;
            }
        }

        #region Append

        public void Append(int n)
        {
            CheckInitialized();

            Frame frame = new Frame(n);

            m_frames.Add(frame);
        }

        public void Append(long n)
        {
            CheckInitialized();

            Frame frame = new Frame(n);

            m_frames.Add(frame);
        }

        public void Append(string s, Encoding encoding)
        {
            CheckInitialized();

            Frame frame = new Frame(s, encoding);

            m_frames.Add(frame);
        }

        public void Append(string s)
        {
            Append(s, Encoding.ASCII);
        }

        public void Append(Guid guid)
        {
            CheckInitialized();

            Frame frame = new Frame(guid);

            m_frames.Add(frame);
        }

        public void Append(byte[] bytes)
        {
            CheckInitialized();

            Frame frame = new Frame(bytes);

            m_frames.Add(frame);
        }

        public void AppendEmpty()
        {
            CheckInitialized();

            Frame frame = new Frame();
            frame.Type = FrameType.Empty;

            m_frames.Add(frame);
        }

        #endregion

        #region Push
        
        public void Push(int n)
        {
            CheckInitialized();

            Frame frame = new Frame(n);

            m_frames.Insert(0, frame);
        }

        public void Push(long n)
        {
            CheckInitialized();

            Frame frame = new Frame(n);

            m_frames.Insert(0, frame);
        }

        public void Push(string s, Encoding encoding)
        {
            CheckInitialized();

            Frame frame = new Frame(s, encoding);

            m_frames.Insert(0, frame);
        }

        public void Push(string s)
        {            
            Push(s, Encoding.ASCII);
        }

        public void Push(Guid guid)
        {
            CheckInitialized();

            Frame frame = new Frame(guid);

            m_frames.Insert(0, frame);
        }

        public void Push(byte[] bytes)
        {
            CheckInitialized();

            Frame frame = new Frame(bytes);

            m_frames.Insert(0, frame);
        }

        public void Push(byte[] bytes, int offset, int count)
        {
            CheckInitialized();

            Frame frame = new Frame(bytes, offset, count);

            m_frames.Insert(0, frame);
        }

        public void PushEmpty()
        {
            CheckInitialized();

            Frame frame = new Frame();
            frame.Type = FrameType.Empty;

            m_frames.Insert(0, frame);
        }

        #endregion

        #region Set

        public void Set(int index, int value)
        {
            CheckInitialized();

            Frame frame = new Frame(value);

            m_frames[index] = frame;
        }

        public void Set(int index, long n)
        {
            CheckInitialized();

            Frame frame = new Frame(n);

            m_frames[index] = frame;
        }

        public void Set(int index, string s, Encoding encoding)
        {
            CheckInitialized();

            Frame frame = new Frame(s, encoding);

            m_frames[index] = frame;
        }

        public void Set(int index, string s)
        {            
            Set(index, s, Encoding.ASCII);
        }

        public void Set(int index, Guid guid)
        {
            CheckInitialized();

            Frame frame = new Frame(guid);

            m_frames[index] = frame;
        }

        public void Set(int index, byte[] bytes)
        {
            CheckInitialized();

            Frame frame = new Frame(bytes);

            m_frames[index] = frame;
        }

        public void Set(int index, byte[] bytes, int offset, int count)
        {
            CheckInitialized();

            Frame frame = new Frame(bytes, offset, count);

            m_frames[index] = frame;
        }

        public void SetEmpty(int index)
        {
            CheckInitialized();

            Frame frame = new Frame();
            frame.Type = FrameType.Empty;

            m_frames[index] = frame;
        }

        #endregion

        #region Convert

        private static int ConvertToInt32(ref Frame frame)
        {
            if (frame.Type == FrameType.Integer)
            {
                return frame.Integer;
            }
            else if (frame.Type == FrameType.ByteArray && frame.Size == 4)
            {
                return NetworkOrderBitConverter.ToInt32(frame.ByteArray, frame.Offset);
            }
            else
            {
                throw new InvalidCastException();
            }
        }

        private long ConvertToInt64(ref Frame frame)
        {
            if (frame.Type == FrameType.Long)
            {
                return frame.Long;
            }
            else if (frame.Type == FrameType.ByteArray && frame.Size == 8)
            {
                return NetworkOrderBitConverter.ToInt64(frame.ByteArray, frame.Offset);
            }
            else
            {
                throw new InvalidCastException();
            }
        }

        private string ConvertToString(Encoding encoding, ref Frame frame)
        {
            if (frame.Type == FrameType.String)
            {
                // TODO: should we through exception of the encoding is different?
                return frame.String;
            }
            else if (frame.Type == FrameType.ByteArray)
            {
                return encoding.GetString(frame.ByteArray, frame.Offset, frame.Size);
            }
            else
            {
                throw new InvalidCastException();
            }
        }

        private Guid ConvertToGuid(ref Frame frame)
        {
            if (frame.Type == FrameType.Guid)
            {
                return frame.Guid;
            }
            else if (frame.Type == FrameType.ByteArray)
            {
                return new Guid(frame.ByteArray);
            }
            else
            {
                throw new InvalidCastException();
            }
        }

        private byte[] ConvertToByteArray(ref Frame frame)
        {
            switch (frame.Type)
            {
                case FrameType.Empty:
                    return new byte[0];
                    break;
                case FrameType.Integer:
                    return NetworkOrderBitConverter.GetBytes(frame.Integer);
                    break;
                case FrameType.Long:
                    return NetworkOrderBitConverter.GetBytes(frame.Long);
                    break;
                case FrameType.String:
                    return frame.Encoding.GetBytes(frame.String);
                    break;
                case FrameType.Guid:
                    return frame.Guid.ToByteArray();
                    break;
                case FrameType.ByteArray:
                    byte[] byteArray = new byte[frame.Size];
                    Buffer.BlockCopy(frame.ByteArray, frame.Offset, byteArray, 0, frame.Size);

                    return byteArray;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static int ConvertToByteArray(byte[] byteArray, int offset, ref Frame frame)
        {
            switch (frame.Type)
            {
                case FrameType.Empty:
                    return 0;
                    break;
                case FrameType.Integer:
                    NetworkOrderBitConverter.PutBytes(byteArray, offset, frame.Integer);
                    return 4;
                    break;
                case FrameType.Long:
                    NetworkOrderBitConverter.PutBytes(byteArray, offset, frame.Long);
                    return 8;
                    break;
                case FrameType.String:
                    return frame.Encoding.GetBytes(frame.String, 0, frame.String.Length, frame.ByteArray, offset);
                    break;
                case FrameType.Guid:
                    byte[] guidBytes = frame.Guid.ToByteArray();

                    Buffer.BlockCopy(guidBytes, 0, byteArray, offset, guidBytes.Length);

                    return guidBytes.Length;
                    break;
                case FrameType.ByteArray:
                    Buffer.BlockCopy(frame.ByteArray, frame.Offset, byteArray, offset, frame.Size);
                    return frame.Size;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region Pop

        public int PeekByteCount()
        {            
            return GetByteCount(0);
        }

        public Int32 PopInt32()
        {
            CheckInitialized();

            Frame frame = m_frames[0];
            m_frames.RemoveAt(0);

            return ConvertToInt32(ref frame);
        }

        public Int64 PopInt64()
        {
            CheckInitialized();

            Frame frame = m_frames[0];
            m_frames.RemoveAt(0);

            return ConvertToInt64(ref frame);
        }

        public string PopString()
        {
            return PopString(Encoding.ASCII);
        }

        public string PopString(Encoding encoding)
        {
            CheckInitialized();

            Frame frame = m_frames[0];
            m_frames.RemoveAt(0);

            return ConvertToString(encoding, ref frame);
        }

        public Guid PopGuid()
        {
            CheckInitialized();

            Frame frame = m_frames[0];
            m_frames.RemoveAt(0);

            return ConvertToGuid(ref frame);
        }

        public byte[] PopByteArray()
        {
            CheckInitialized();

            Frame frame = m_frames[0];
            m_frames.RemoveAt(0);

            return ConvertToByteArray(ref frame);
        }

        public int PopByteArray(byte[] byteArray, int offset)
        {
            CheckInitialized();

            Frame frame = m_frames[0];
            m_frames.RemoveAt(0);

            return ConvertToByteArray(byteArray, offset, ref frame);
        }

        public void PopEmpty()
        {
            CheckInitialized();

            Frame frame = m_frames[0];
            m_frames.RemoveAt(0);

            if (frame.Type != FrameType.Empty && !(frame.Type == FrameType.ByteArray && frame.Size == 0))
            {
                // TODO: what should ex throw here?
                throw new InvalidCastException();
            }
        }

        #endregion

        #region Get

        public int GetByteCount(int index)
        {
            CheckInitialized();

            switch (m_frames[index].Type)
            {
                case FrameType.Empty:
                    return 0;
                    break;
                case FrameType.Integer:
                    return 4;
                    break;
                case FrameType.Long:
                    return 8;
                    break;
                case FrameType.String:
                    return m_frames[index].Encoding.GetByteCount(m_frames[index].String);
                    break;
                case FrameType.Guid:
                    return 16;
                    break;
                case FrameType.ByteArray:
                    return m_frames[index].Size;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Int32 GetInt32(int index)
        {
            CheckInitialized();

            Frame frame = m_frames[index];

            return ConvertToInt32(ref frame);
        }

        public Int64 GetInt64(int index)
        {
            CheckInitialized();

            Frame frame = m_frames[index];

            return ConvertToInt64(ref frame);
        }

        public string GetString(int index)
        {
            CheckInitialized();

            return GetString(index, Encoding.ASCII);
        }

        public string GetString(int index, Encoding encoding)
        {
            CheckInitialized();

            Frame frame = m_frames[index];

            return ConvertToString(encoding, ref frame);
        }

        public Guid GetGuid(int index)
        {
            CheckInitialized();

            Frame frame = m_frames[index];

            return ConvertToGuid(ref frame);
        }

        public byte[] GetByteArray(int index)
        {
            CheckInitialized();

            Frame frame = m_frames[index];

            return ConvertToByteArray(ref frame);
        }

        public int GetByteArray(int index, byte[] byteArray, int offset)
        {
            CheckInitialized();

            Frame frame = m_frames[index];

            return ConvertToByteArray(byteArray, offset, ref frame);
        }

        #endregion
    }
}
