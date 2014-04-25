using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetMessage.Core.AsyncIO
{
    public class AsyncOperation : StateMachine
    {
        public const int DoneEvent = 1;
        public const int ErrorEvent = 2;
        

        enum State
        {
            Idle = 1, Pending, Waiting
        }

        private int m_state;
        private bool m_zeroIsError;        
        
        public AsyncOperation(int sourceId, StateMachine owner)  : base(sourceId, owner)
        {
            SourceId = sourceId;
            Owner = owner;
            m_state = (int)State.Idle;

            SocketAsyncEventArgs = new SocketAsyncEventArgs();
            SocketAsyncEventArgs.Completed += OnCompleted;
        }       

        public int SourceId { get; private set; }

        public StateMachine Owner { get; private set; }

        public SocketAsyncEventArgs SocketAsyncEventArgs { get; private set; }

        public bool IsIdle
        {
            get { return m_state == (int)State.Idle; }
        }       

        public override void Dispose()
        {
            SocketAsyncEventArgs.Completed -= OnCompleted;
            SocketAsyncEventArgs.Dispose();
        }

        public void Start(bool zeroIsError)
        {
            Interlocked.Exchange(ref m_state, (int) State.Pending);
            m_zeroIsError = false;

        }

        public void Stop()
        {
            Interlocked.Exchange(ref m_state, (int)State.Idle);
        }

        public void Waiting(bool feed=true)
        {
            State state = (State)Interlocked.CompareExchange(ref m_state, (int)State.Waiting, (int)State.Pending);

            if (feed)
            {
                if (state == State.Idle)
                {
                    int action = DoneEvent;

                    if (SocketAsyncEventArgs.SocketError != SocketError.Success ||
                        (m_zeroIsError && SocketAsyncEventArgs.BytesTransferred == 0))
                    {
                        action = ErrorEvent;
                    }

                    Owner.Feed(SourceId, action, this);
                }
            }            
        }

        private void OnCompleted(object sender, SocketAsyncEventArgs e)
        {
            // if the operation completed synced
            if (Interlocked.CompareExchange(ref m_state, (int)State.Idle, (int)State.Pending) == (int)State.Pending)
            {               
                // async operation canceled 
                return;
            }

            Owner.Context.Enter();            

            try
            {               
                Debug.Assert(m_state != (int)State.Idle);
            
                int action = DoneEvent;

                if (e.SocketError != SocketError.Success ||
                    (m_zeroIsError && e.BytesTransferred == 0))
                {
                    action = ErrorEvent;
                }

                Interlocked.Exchange(ref m_state, (int) State.Idle);                

                Owner.Feed(SourceId, action, this);
            }
            finally
            {
                Owner.Context.Leave();
            }
        }

        protected override void Handle(int sourceId, int type, StateMachine source)
        {            
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {            
        }        
    }
}
