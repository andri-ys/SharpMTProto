// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ITransport.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace SharpMTProto.Transport
{
    public enum TransportState
    {
        Disconnected = 0,
        Connected = 1
    }

    public interface ITransport : ISubject<byte[]>, IDisposable
    {
        bool IsConnected { get; }
        TransportState State { get; }
        Task Connect();
        Task Connect(CancellationToken token);

        Task Disconnect();
        Task Disconnect(CancellationToken token);
    }
}
