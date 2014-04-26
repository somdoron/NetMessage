﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetMessage.Core.Core;

namespace NetMessage.NetMQ.Patterns
{
    public class Request : Dealer
    {
        public class RequestSocketType : SocketType<NetMQMessage>
        {
            public RequestSocketType(SocketTypeFlags flags)
                : base(SocketTypes.Request, flags)
            {
            }

            public override SocketBase<NetMQMessage> Create(object hint)
            {
                return new Request(hint);
            }

            public override bool IsPeer(int socketType)
            {
                return socketType == SocketTypes.Response || socketType == SocketTypes.Router;
            }
        }

        //  If true, request was already sent and reply wasn't received yet or
        //  was raceived partially.
        private bool m_receivingReply;

        public Request(object hint)
            : base(hint)
        {
            m_receivingReply = false;
        }

        protected override SendReceiveResult Send(NetMQMessage message)
        {
            //  If we've sent a request and we still haven't got the reply,
            //  we can't send another request.
            if (m_receivingReply)
            {
                throw new InvalidOperationException("Cannot send another request");
            }

            // add the bottom to the begining of the message
            message.PushEmptyFrame();

            var sendResult = base.Send(message);

            if (sendResult == SendReceiveResult.Ok)
            {
                m_receivingReply = true;
            }

            return sendResult;
        }

        protected override SendReceiveResult Receive(out NetMQMessage message)
        {
            if (!m_receivingReply)
            {
                throw new InvalidOperationException("Must send a request first");
            }

            var receiveResult = base.Receive(out message);

            if (receiveResult != SendReceiveResult.Ok)
            {
                return receiveResult;
            }

            // the first frame must be zero size and not the last frame
            var frame = message.Pop();

            if (frame.MessageSize != 0 || message.FrameCount == 0)
            {
                Debug.Assert(false);
                message = null;
                return SendReceiveResult.ShouldTryAgain;
            }

            m_receivingReply = false;
            return SendReceiveResult.Ok;
        }

        protected override SocketEvents Events
        {
            get
            {
                return base.Events & (m_receivingReply ? SocketEvents.In : SocketEvents.Out);
            }
        }
    }
}
