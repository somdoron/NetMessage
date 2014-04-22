using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core;

namespace NetMessage.Patterns.Utils
{
    public class FairQueuing
    {
        public class Data
        {
            public PriorityList.Data PriorityListData { get; set; }
        }

        private PriorityList m_priorityList;

        public FairQueuing()
        {
            m_priorityList = new PriorityList();
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

        public void In(Data data)
        {
            m_priorityList.Activate(data.PriorityListData);
        }

        public SendReceiveResult Receive(out Message message)
        {
            IPipe pipe;

            return Receive(out message, out pipe);
        }

        public SendReceiveResult Receive(out Message message, out IPipe pipe)
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
