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
        void SendPlainMessage(byte[] messageData);
        void SendMessage(IMessage message);
        void SendEncryptedMessage(byte[] messageData, bool isContentRelated = true);
        void SetupEncryption(byte[] authKey, ulong salt);
        bool IsEncryptionSupported { get; }
        
        /// <summary>
        ///     Sends plain (unencrypted) message and waits for a response.
        /// </summary>
        /// <typeparam name="TResponse">Type of the response which will be awaited.</typeparam>
        /// <param name="requestMessageDataObject">Request message data.</param>
        /// <param name="timeout">Timeout.</param>
        /// <returns>Response.</returns>
        /// <exception cref="TimeoutException">When response is not captured within a specified timeout.</exception>
        Task<TResponse> SendPlainMessage<TResponse>(object requestMessageDataObject, TimeSpan timeout);
        
        /// <summary>
        ///     Sends encrypted message and waits for a response.
        /// </summary>
        /// <typeparam name="TResponse">Type of the response which will be awaited.</typeparam>
        /// <param name="requestMessageDataObject">Request message data.</param>
        /// <param name="timeout">Timeout.</param>
        /// <returns>Response.</returns>
        /// <exception cref="TimeoutException">When response is not captured within a specified timeout.</exception>
        Task<TResponse> SendEncryptedMessage<TResponse>(object requestMessageDataObject, TimeSpan timeout);
        
        /// <summary>
        /// Diconnect.
        /// </summary>
        Task Disconnect();

        /// <summary>
        ///     Connect.
        /// </summary>
        Task<MTProtoConnectResult> Connect();

        /// <summary>
        ///     Connect.
        /// </summary>
        Task<MTProtoConnectResult> Connect(CancellationToken cancellationToken);

        Task<TResponse> SendMessage<TResponse>(object requestMessageDataObject, TimeSpan timeout, MessageType messageType);
        Task<TResponse> SendEncryptedMessage<TResponse>(object requestMessageDataObject);
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
