// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SocketAwaitable.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SharpMTProto.Extra
{
    public sealed class SocketAwaitable : INotifyCompletion
    {
        private static readonly Action Sentinel = () => { };

        internal readonly SocketAsyncEventArgs EventArgs;
        internal Action Continuation;
        internal bool WasCompleted;

        public SocketAwaitable(SocketAsyncEventArgs eventArgs)
        {
            if (eventArgs == null)
            {
                throw new ArgumentNullException("eventArgs");
            }
            EventArgs = eventArgs;
            eventArgs.Completed += delegate
            {
                Action prev = Continuation ?? Interlocked.CompareExchange(ref Continuation, Sentinel, null);
                if (prev != null)
                {
                    prev();
                }
            };
        }

        public bool IsCompleted
        {
            get { return WasCompleted; }
        }

        public void OnCompleted(Action continuation)
        {
            if (Continuation == Sentinel || Interlocked.CompareExchange(ref Continuation, continuation, null) == Sentinel)
            {
                Task.Run(continuation);
            }
        }

        internal void Reset()
        {
            WasCompleted = false;
            Continuation = null;
        }

        public SocketAwaitable GetAwaiter()
        {
            return this;
        }

        public void GetResult()
        {
            if (EventArgs.SocketError != SocketError.Success)
            {
                throw new SocketException((int) EventArgs.SocketError);
            }
        }
    }
}
