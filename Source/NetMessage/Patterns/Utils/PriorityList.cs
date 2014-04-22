using System.Collections.Generic;
using System.Diagnostics;
using NetMessage.Core.Core;

namespace NetMessage.Core.Patterns.Utils
{
    public class PriorityList
    {        
        public class Data
        {
            public IPipe Pipe { get; set; }

            public int Priority { get; set; }            
        }

        class Slot
        {
            public Slot()
            {
                Pipes = new List<Data>();
                Current = null;
            }

            public IList<Data> Pipes { get; private set; } 

            public Data Current { get; set; }
        }

        public const int Slots = 16;

        Slot[] m_slots = new Slot[Slots];

        private int m_current;      

        public PriorityList()
        {
            m_current = -1;

            for (int i = 0; i < Slots; i++)
            {
                m_slots[i] = new Slot();
            }
        }

        private Slot CurrentSlot
        {
            get
            {
                return m_current >= 0 ? m_slots[m_current - 1] : null;
            }
        }

        public int Priority
        {
            get
            {
                return m_current;
            }
        }

        public IPipe Pipe            
        {
            get
            {
                var slot = CurrentSlot;

                if (slot == null)
                    return null;

                return slot.Current.Pipe;    
            }            
        }

        public bool IsActive
        {
            get
            {
                return m_current != -1;
            }
        }      

        public Data Add(IPipe pipe, int priority)
        {
            Data data = new Data();
            data.Pipe = pipe;
            data.Priority = priority;

            return data;
        }

        public void Remove(Data data)
        {
            Slot slot = m_slots[data.Priority - 1];

            if (slot.Pipes.Contains(data))
            {
                if (slot.Current != data)
                {
                    slot.Pipes.Remove(data);
                }
                else
                {
                    if (slot.Pipes.Count > 1)
                    {
                        // Advance the current pointer (with wrap-over).
                        int index = (slot.Pipes.IndexOf(data) + 1)%slot.Pipes.Count;
                        slot.Current = slot.Pipes[index];
                    }
                    else
                    {
                        slot.Current = null;
                    }

                    slot.Pipes.Remove(data);

                    if (m_current != data.Priority)
                    {
                        // the current slot may have become empty and we have switch
                        // to lower priority slots.

                        while (CurrentSlot.Pipes.Count == 0)
                        {
                            m_current++;

                            if (m_current > Slots)
                            {
                                m_current = -1;
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void Activate(Data data)
        {
            Slot slot = m_slots[data.Priority - 1];

            //  If there are already some elements in this slot, current pipe is not
            // going to change.
            if (slot.Pipes.Count > 0)
            {
                slot.Pipes.Add(data);
            }
            else
            {
                // Add first pipe into the slot. If there are no pipes in priolist at all
                // this slot becomes current.
                slot.Pipes.Add(data);
                slot.Current = data;

                if (m_current == -1 || m_current > data.Priority)
                {
                    m_current = data.Priority;
                }                
            }
        }

        public void Advance(bool release)
        {
            Slot slot = CurrentSlot;

            Debug.Assert(slot != null);

            var currentPipe = slot.Current;
            
            int index = (slot.Pipes.IndexOf(slot.Current) + 1)%slot.Pipes.Count;

            if (!release || slot.Pipes.Count > 1)
            {
                slot.Current = slot.Pipes[index];    
            }
            else
            {
                slot.Current = null;
            }            

            if (release)
            {
                slot.Pipes.Remove(currentPipe);
             
                while (CurrentSlot.Pipes.Count == 0)
                {
                    m_current++;

                    if (m_current > Slots)
                    {
                        m_current = -1;
                        break;
                    }
                }
            }
        }
    }
}
