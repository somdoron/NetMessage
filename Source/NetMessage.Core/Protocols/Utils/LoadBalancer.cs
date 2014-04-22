﻿using NetMessage.Core.Core;

namespace NetMessage.Core.Protocols.Utils
{
    public class LoadBalancer<T> where T : MessageBase
    {
         public class Data
        {
            public PriorityList<T>.Data PriorityListData { get; set; }
        }

        private PriorityList<T> m_priorityList;

        public LoadBalancer()
        {
            m_priorityList = new PriorityList<T>();
        }

        public bool CanSend
        {
            get
            {
                return m_priorityList.IsActive;
            }
        }

        public int Priority
        {
            get
            {
                return m_priorityList.Priority;
            }
        }

        public Data Add(IPipe<T> pipe, int priority)
        {
            Data data = new Data();
            data.PriorityListData = m_priorityList.Add(pipe, priority);

            return data;
        }

        public void Remove(Data data)
        {
            m_priorityList.Remove(data.PriorityListData);
        }

        public void Out(Data data)
        {
            m_priorityList.Activate(data.PriorityListData);
        }

        public SendReceiveResult Send(T message)
        {
            IPipe<T> pipe;
            return Send(message, out pipe);
        }

        public SendReceiveResult Send(T message, out IPipe<T> pipe)
        {
            pipe = m_priorityList.Pipe;

            if (pipe == null)
                return SendReceiveResult.ShouldTryAgain;

            var pipeStatus = pipe.Send(message);

            m_priorityList.Advance(pipeStatus == PipeStatus.Release);

            return SendReceiveResult.Ok;            
        }
    }
}