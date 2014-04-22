using NetMessage.Core.Core;

namespace NetMessage.Core.Protocols.Utils
{
    public class FairQueuing<T> where T : MessageBase
    {
        public class Data
        {
            public PriorityList<T>.Data PriorityListData { get; set; }
        }

        private PriorityList<T> m_priorityList;

        public FairQueuing()
        {
            m_priorityList = new PriorityList<T>();
        }

        public bool CanReceive
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

        public void In(Data data)
        {
            m_priorityList.Activate(data.PriorityListData);
        }

        public SendReceiveResult Receive(out T message)
        {
            IPipe<T> pipe;

            return Receive(out message, out pipe);
        }

        public SendReceiveResult Receive(out T message, out IPipe<T> pipe)
        {
            pipe = m_priorityList.Pipe;

            if (pipe == null)
            {
                message = null;
                return SendReceiveResult.ShouldTryAgain;
            }
                
            var pipeStatus = pipe.Receive(out message);

            m_priorityList.Advance(pipeStatus == PipeStatus.Release);

            return SendReceiveResult.Ok;
        }
    }
}
