using NetMessage.Core.Core;

namespace NetMessage.Core.Patterns.Utils
{
    public class LoadBalancer
    {
         public class Data
        {
            public PriorityList.Data PriorityListData { get; set; }
        }

        private PriorityList m_priorityList;

        public LoadBalancer()
        {
            m_priorityList = new PriorityList();
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

        public Data Add(IPipe pipe, int priority)
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

        public SendReceiveResult Send(Message message)
        {
            IPipe pipe;
            return Send(message, out pipe);
        }

        public SendReceiveResult Send(Message message, out IPipe pipe)
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
