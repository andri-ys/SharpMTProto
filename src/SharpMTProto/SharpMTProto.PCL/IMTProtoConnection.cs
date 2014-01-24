// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IMTProtoConnection.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using MTProtoSchema;

namespace SharpMTProto
{
    public interface IMTProtoConnection : ITLAsyncMethods, IDisposable
    {
        IObservable<IMessage> InMessagesHistory { get; }
        IObservable<IMessage> OutMessagesHistory { get; }
        MTProtoConnectionState State { get; }
        bool IsConnected { get; }
        TimeSpan DefaultRpcTimeout { get; set; }
        TimeSpan DefaultConnectTimeout { get; set; }
        void SendPlainMessage(PlainMessage message);
        void SendPlainMessage(byte[] messageData);

        /// <summary>
        ///     Sends plain (unencrypted) message and waits for a response.
        /// </summary>
        /// <typeparam name="TResponse">Type of the response which will be awaited.</typeparam>
        /// <param name="requestMessageData">Request message data.</param>
        /// <param name="timeout">Timeout.</param>
        /// <returns>Response.</returns>
        /// <exception cref="TimeoutException">When response is not captured within a specified timeout.</exception>
        Task<TResponse> SendPlainMessage<TResponse>(object requestMessageData, TimeSpan timeout) where TResponse : class;

        Task Disconnect();

        /// <summary>
        ///     Connect.
        /// </summary>
        Task<MTProtoConnectResult> Connect();

        /// <summary>
        ///     Connect.
        /// </summary>
        Task<MTProtoConnectResult> Connect(CancellationToken cancellationToken);
    }

    public enum MTProtoConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2
    }

    public enum MTProtoConnectResult
    {
        Success,
        Timeout,
        Other
    }
}
