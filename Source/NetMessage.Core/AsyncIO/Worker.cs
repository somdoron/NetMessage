using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncIO;

namespace NetMessage.Core.AsyncIO
{
    class Worker : IDisposable
    {
        private Thread m_thread;
        private Timerset m_timerset;

        private object m_stopTask = new object();

        public Worker()
        {
            CompletionPort = CompletionPort.Create();

            m_thread = new Thread(Run);
            m_thread.Start();
        }

        public CompletionPort CompletionPort { get; private set; }

        public void Dispose()
        {
            CompletionPort.Signal(m_stopTask);

            m_thread.Join();

            CompletionPort.Dispose();
        }

        public void Execute(WorkerTask task)
        {
            CompletionPort.Signal(task);
        }

        public void AddTimer(int timeout, Timer timer)
        {
            m_timerset.Add(timeout, timer);
        }

        public void RemoveTimer(Timer timer)
        {
            m_timerset.Remove(timer);
        }

        private void Run()
        {
            int timeout;

            CompletionStatus completionStatus;

            while (true)
            {

                while (true)
                {
                    Timer timer;

                    // get elapsed timers
                    if (m_timerset.TryGetEvent(out timer))
                    {
                        timer.Context.Enter();
                        try
                        {
                            timer.Feed(StateMachine.ActionSourceId, Timer.TimeOutAction, null);
                        }
                        finally
                        {
                            timer.Context.Leave();
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                // Compute the time interval till next timer expiration.
                timeout = m_timerset.Timeout();

                if (CompletionPort.GetQueuedCompletionStatus(timeout, out completionStatus))
                {
                    if (completionStatus.OperationType == OperationType.Signal)
                    {
                        if (object.ReferenceEquals(m_stopTask, completionStatus.State))
                        {
                            // Worker thread shutdown is requested.   
                            return;
                        }
                        else
                        {
                            WorkerTask workerTask = (WorkerTask)completionStatus.State;

                            workerTask.Context.Enter();
                            try
                            {
                                workerTask.Owner.Feed(workerTask.SourceId, WorkerTask.ExecuteEvent, workerTask);
                            }
                            finally
                            {
                                workerTask.Context.Leave();
                            }
                        }
                    }
                    else
                    {
                        USocket socket = (USocket)completionStatus.State;

                        socket.Context.Enter();
                        try
                        {
                            int type;
                            WorkerOperation operation;

                            switch (completionStatus.OperationType)
                            {
                                case OperationType.Accept:
                                    operation = socket.In;
                                    type = completionStatus.SocketError == SocketError.Success
                                        ? WorkerOperation.DoneEvent
                                        : WorkerOperation.ErrorEvent;
                                    break;
                                case OperationType.Connect:
                                    operation = socket.Out;
                                    type = completionStatus.SocketError == SocketError.Success
                                        ? WorkerOperation.DoneEvent
                                        : WorkerOperation.ErrorEvent;
                                    break;
                                case OperationType.Receive:
                                    operation = socket.In;
                                    type = completionStatus.SocketError == SocketError.Success &&
                                           completionStatus.BytesTransferred > 0
                                        ? WorkerOperation.DoneEvent
                                        : WorkerOperation.ErrorEvent;
                                    socket.BytesReceived = completionStatus.BytesTransferred;
                                    break;
                                case OperationType.Send:
                                    operation = socket.Out;
                                    type = completionStatus.SocketError == SocketError.Success &&
                                           completionStatus.BytesTransferred > 0
                                        ? WorkerOperation.DoneEvent
                                        : WorkerOperation.ErrorEvent;
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                            
                            operation.Stop();

                            socket.Feed(operation.SourceId, type, operation);
                        }
                        finally
                        {
                            socket.Context.Leave();
                        }
                    }
                }
            }
        }
    }
}
