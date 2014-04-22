using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetMessage.AsyncIO
{
    public class AsyncOperation : StateMachine
    {
        public int ErrorEvent = 2;
        public int DoneEvent = 2;

        enum State
        {
            Idle = 1, Active, ActiveZeroIsError   
        }

        private State m_state;

        public AsyncOperation(int sourceId, StateMachine owner)  : base(sourceId, owner)
        {
            SourceId = sourceId;
            Owner = owner;
            m_state = State.Idle;

            SocketAsyncEventArgs = new SocketAsyncEventArgs();
            SocketAsyncEventArgs.Completed += OnCompleted;
        }       

        public int SourceId { get; private set; }

        public StateMachine Owner { get; private set; }

        public SocketAsyncEventArgs SocketAsyncEventArgs { get; private set; }

        public bool IsIdle
        {
            get { return m_state == State.Idle; }
        }

        public override void Dispose()
        {
            SocketAsyncEventArgs.Completed -= OnCompleted;
            SocketAsyncEventArgs.Dispose();
        }

        public void Start(bool zeroIsError)
        {
            m_state = zeroIsError ? State.ActiveZeroIsError : State.Active;
        }

        private void OnCompleted(object sender, SocketAsyncEventArgs e)
        {
            Owner.Context.Enter();

            try
            {
                Debug.Assert(m_state != State.Idle);

                int action = DoneEvent;

                if (e.SocketError != SocketError.Success ||
                    (m_state == State.ActiveZeroIsError && e.BytesTransferred == 0))
                {
                    action = ErrorEvent;
                }

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
