using NetMessage.Core.AsyncIO;

namespace NetMessage.Core.Transport.Tcp
{
    public class AcceptedConnection : StateMachine
    {
        public int SourceId = 20;

        internal void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public bool IsIdle
        {
            get
            {
                return true;
            }
        }

        internal void Stop()
        {
            throw new System.NotImplementedException();
        }

        protected override void Handle(int sourceId, int type, StateMachine source)
        {
            throw new System.NotImplementedException();
        }

        protected override void Shutdown(int sourceId, int type, StateMachine source)
        {
            throw new System.NotImplementedException();
        }
    }
}
