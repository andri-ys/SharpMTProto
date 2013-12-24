// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IConnector.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace SharpMTProto
{
    public enum ConnectorState
    {
        Disconnected = 0,
        Connected = 1
    }

    public interface IConnector : ISubject<byte[]>, IDisposable
    {
        bool IsConnected { get; }
        ConnectorState State { get; }
        Task Connect(TimeSpan timeout);
        Task Connect(TimeSpan timeout, CancellationToken token);
    }
}
