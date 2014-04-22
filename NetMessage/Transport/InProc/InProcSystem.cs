using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.Transport.InProc
{
    public class InProcSystem : IDisposable
    {
        private static InProcSystem s_instance = new InProcSystem();

        private object m_sync;

        private List<BoundEndpoint> m_bound;
        private List<ConnectEndpoint> m_connected; 

        private InProcSystem()
        {
            
        }

        public static InProcSystem Instance
        {
            get
            {
                return s_instance;
            }
        }

        public void Init()
        {
            m_sync = new object();
            m_bound = new List<BoundEndpoint>();
            m_connected = new List<ConnectEndpoint>();
        }

        public void Dispose()
        {
            
        }

        public void Bind(BoundEndpoint bindEndpoint, Action<ConnectEndpoint> callback)
        {
            lock (m_sync)
            {
                if (m_bound.Exists(b => b.Address == bindEndpoint.Address))
                {
                    throw new NetMessageException(NetMessageErrorCode.AddressInUse);
                }

                m_bound.Add(bindEndpoint);

                foreach (ConnectEndpoint connectEndpoint in m_connected)
                {
                    if (connectEndpoint.Address == bindEndpoint.Address)
                    {
                        if (bindEndpoint.IsPeer(connectEndpoint.SocketType))
                        {                            
                            callback(connectEndpoint);
                        }
                    }
                }
            }
        }

        public void Connect(ConnectEndpoint item, Action<BoundEndpoint> callback)
        {
            lock (m_sync)
            {
                m_connected.Add(item);

                BoundEndpoint boundEndpoint = m_bound.FirstOrDefault(b => b.Address ==
                                                                   item.Address);

                if (boundEndpoint != null && item.IsPeer(boundEndpoint.SocketType))
                {
                    boundEndpoint.Connects++;
                    callback(boundEndpoint);
                }
            }
        }

        public void Disconnect(ConnectEndpoint endpoint)
        {
            lock (m_sync)
            {
                m_connected.Remove(endpoint);
            }
        }

        public void Unbind(BoundEndpoint endpoint)
        {
            lock (m_sync)
            {
                m_bound.Remove(endpoint);
            }
        }
    }
}
