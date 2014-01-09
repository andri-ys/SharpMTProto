// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ITransport.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharpMTProto.Transport
{
    public enum TransportState
    {
        Disconnected = 0,
        Connected = 1
    }

    public interface ITransport : IObservable<byte[]>, IDisposable
    {
        bool IsConnected { get; }
        TransportState State { get; }
        void Connect();
        Task ConnectAsync();
        Task ConnectAsync(CancellationToken token);
        void Disconnect();
        Task DisconnectAsync();
        Task DisconnectAsync(CancellationToken token);
        void Send(byte[] payload);
        Task SendAsync(byte[] payload);
        Task SendAsync(byte[] payload, CancellationToken token);
    }
}
