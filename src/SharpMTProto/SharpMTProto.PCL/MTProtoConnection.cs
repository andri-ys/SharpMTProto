// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnection.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel;
using Catel.Logging;
using MTProtoSchema;
using SharpMTProto.Annotations;
using SharpMTProto.Services;
using SharpMTProto.Transport;
using SharpTL;
using AsyncLock = Nito.AsyncEx.AsyncLock;

namespace SharpMTProto
{
    /// <summary>
    ///     MTProto connection.
    /// </summary>
    public class MTProtoConnection : IMTProtoConnection
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly AsyncLock _lock = new AsyncLock();
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly TLRig _tlRig;
        private readonly ITransport _transport;
        private readonly ITransportFactory _transportFactory;
        private CancellationToken _connectionCancellationToken;

        private CancellationTokenSource _connectionCts;

        private bool _isDisposed;

        private volatile MTProtoConnectionState _state = MTProtoConnectionState.Disconnected;

        #region Message hubs.
        private Subject<IMessage> _inMessages = new Subject<IMessage>();
        private ReplaySubject<IMessage> _inMessagesHistory = new ReplaySubject<IMessage>(100);
        private Subject<IMessage> _outMessages = new Subject<IMessage>();
        private ReplaySubject<IMessage> _outMessagesHistory = new ReplaySubject<IMessage>(100);
        #endregion

        public MTProtoConnection(TransportConfig transportConfig, [NotNull] ITransportFactory transportFactory, [NotNull] TLRig tlRig,
            [NotNull] IMessageIdGenerator messageIdGenerator)
        {
            Argument.IsNotNull(() => transportFactory);
            Argument.IsNotNull(() => tlRig);
            Argument.IsNotNull(() => messageIdGenerator);

            _transportFactory = transportFactory;
            _tlRig = tlRig;
            _messageIdGenerator = messageIdGenerator;

            DefaultRpcTimeout = Defaults.RpcTimeout;
            DefaultConnectTimeout = Defaults.ConnectTimeout;

            // Init transport.
            _transport = _transportFactory.CreateTransport(transportConfig);

            // History of messages in/out.
            _inMessages.ObserveOn(DefaultScheduler.Instance).Subscribe(_inMessagesHistory);
            _outMessages.ObserveOn(DefaultScheduler.Instance).Subscribe(_outMessagesHistory);

            // Connector in/out.
            _transport.ObserveOn(DefaultScheduler.Instance).Do(bytes => LogMessageInOut(bytes, "IN")).Subscribe(ProcessIncomingMessageBytes);
            _outMessages.ObserveOn(DefaultScheduler.Instance)
                .Do(message => LogMessageInOut(message.MessageBytes, "OUT"))
                .Subscribe(message => _transport.Send(message.MessageBytes));
        }

        public TimeSpan DefaultRpcTimeout { get; set; }
        public TimeSpan DefaultConnectTimeout { get; set; }

        public MTProtoConnectionState State
        {
            get { return _state; }
        }

        public bool IsConnected
        {
            get { return _state == MTProtoConnectionState.Connected; }
        }

        public IObservable<IMessage> InMessagesHistory
        {
            get { return _inMessagesHistory; }
        }

        public IObservable<IMessage> OutMessagesHistory
        {
            get { return _outMessagesHistory; }
        }

        public void SendUnencryptedMessage(byte[] messageData)
        {
            SendUnencryptedMessage(new UnencryptedMessage(GetNextMessageId(), messageData));
        }

        public void SendUnencryptedMessage(UnencryptedMessage message)
        {
            _outMessages.OnNext(message);
        }

        /// <summary>
        ///     Sends unencrypted message and waits for a response.
        /// </summary>
        /// <typeparam name="TResponse">Type of the response which will be awaited.</typeparam>
        /// <param name="requestMessageData">Request message data.</param>
        /// <param name="timeout">Timeout.</param>
        /// <returns>Response.</returns>
        /// <exception cref="TimeoutException">When response is not captured within a specified timeout.</exception>
        public async Task<TResponse> SendUnencryptedMessageAndWaitForResponse<TResponse>(object requestMessageData, TimeSpan timeout) where TResponse : class
        {
            Task<TResponse> resultTask =
                _inMessages.Where(message => message is UnencryptedMessage)
                    .Select(m => _tlRig.Deserialize<TResponse>(((UnencryptedMessage) m).GetMessageData()))
                    .Where(r => r != null)
                    .FirstAsync()
                    .Timeout(timeout)
                    .ToTask(_connectionCancellationToken);

            SendUnencryptedMessage(_tlRig.Serialize(requestMessageData));

            return await resultTask;
        }

        /// <summary>
        ///     Start sender and receiver tasks.
        /// </summary>
        public async Task<MTProtoConnectResult> Connect()
        {
            return await Connect(CancellationToken.None);
        }

        /// <summary>
        ///     Connect.
        /// </summary>
        public async Task<MTProtoConnectResult> Connect(CancellationToken cancellationToken)
        {
            var result = MTProtoConnectResult.Other;

            await Task.Run(async () =>
            {
                using (await _lock.LockAsync(cancellationToken))
                {
                    if (_state == MTProtoConnectionState.Connected)
                    {
                        result = MTProtoConnectResult.Success;
                        return;
                    }
                    Debug.Assert(_state == MTProtoConnectionState.Disconnected);
                    try
                    {
                        _state = MTProtoConnectionState.Connecting;
                        Log.Debug("Connecting...");

                        await _transport.ConnectAsync(cancellationToken).ToObservable().Timeout(DefaultConnectTimeout);

                        _connectionCts = new CancellationTokenSource();
                        _connectionCancellationToken = _connectionCts.Token;

                        Log.Debug("Connected.");
                        result = MTProtoConnectResult.Success;
                    }
                    catch (TimeoutException)
                    {
                        result = MTProtoConnectResult.Timeout;
                        Log.Debug(string.Format("Failed to connect due to timeout ({0}s).", DefaultConnectTimeout.TotalSeconds));
                    }
                    catch (Exception e)
                    {
                        result = MTProtoConnectResult.Other;
                        Log.Debug(e, "Failed to connect.");
                    }
                    finally
                    {
                        switch (result)
                        {
                            case MTProtoConnectResult.Success:
                                _state = MTProtoConnectionState.Connected;
                                break;
                            case MTProtoConnectResult.Timeout:
                            case MTProtoConnectResult.Other:
                                _state = MTProtoConnectionState.Disconnected;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        public async Task Disconnect()
        {
            await Task.Run(async () =>
            {
                using (await _lock.LockAsync())
                {
                    if (_state == MTProtoConnectionState.Disconnected)
                    {
                        return;
                    }
                    _state = MTProtoConnectionState.Disconnected;

                    if (_connectionCts != null)
                    {
                        _connectionCts.Cancel();
                        _connectionCts.Dispose();
                        _connectionCts = null;
                    }

                    await _transport.DisconnectAsync(CancellationToken.None);
                }
            }).ConfigureAwait(false);
        }

        private static void LogMessageInOut(byte[] messageBytes, string inOrOut)
        {
            Log.Debug(string.Format("{0} ({1} bytes): {2}", inOrOut, messageBytes.Length, messageBytes.ToHexString(spaceEveryByte: false)));
        }

        /// <summary>
        ///     Processes incoming message bytes.
        /// </summary>
        /// <param name="bytes">Incoming bytes.</param>
        private async void ProcessIncomingMessageBytes(byte[] bytes)
        {
            TLStreamer streamer = null;
            try
            {
                Log.Debug("Processing incoming message.");
                streamer = new TLStreamer(bytes);
                if (bytes.Length == 4)
                {
                    int error = streamer.ReadInt();
                    Log.Debug("Received error code: {0}.", error);
                    return;
                }
                else if (bytes.Length < 20)
                {
                    throw new InvalidMessageException(
                        string.Format("Invalid message length: {0} bytes. Expected to be at least 20 bytes for message or 4 bytes for error code.", bytes.Length));
                }

                ulong authKeyId = streamer.ReadULong();
                Log.Debug(string.Format("Auth key ID = {0:X16}.", authKeyId));
                if (authKeyId == 0)
                {
                    // Assume the message bytes has an unencrypted message.
                    Log.Debug(string.Format("Assume this is an unencrypted message."));

                    // Reading message ID.
                    ulong messageId = streamer.ReadULong();
                    if (!IsIncomingMessageIdValid(messageId))
                    {
                        throw new InvalidMessageException(string.Format("Message ID = {0:X16} is invalid.", messageId));
                    }

                    // Reading message data length.
                    int messageDataLength = streamer.ReadInt();
                    if (messageDataLength <= 0)
                    {
                        throw new InvalidMessageException("Message data length must be greater than zero.");
                    }

                    // Reading message data.
                    var messageData = new byte[messageDataLength]; // TODO: consider reusing of byte arrays.
                    int read = await streamer.ReadAsync(messageData, 0, messageDataLength, _connectionCancellationToken);
                    if (read != messageDataLength)
                    {
                        throw new InvalidMessageException(string.Format("Actual message data length ({0}) is not as expected ({1}).", read, messageDataLength));
                        // TODO: read message data if read is less than expected.
                    }

                    // Notify in-messages subject.
                    var message = new UnencryptedMessage(messageId, messageData);

                    Log.Debug(string.Format("Received unencrypted message. Message ID = {0:X16}. Message data length: {1} bytes.", messageId, messageDataLength));

                    _inMessages.OnNext(message);
                }
                else
                {
                    // Assume the stream has an encrypted message.
                    Log.Debug(string.Format("Auth key ID = {0:X16}. Assume this is encrypted message. (Encrypted messages NOT supported yet. Skipping.)", authKeyId));
                }
            }
            catch (InvalidMessageException e)
            {
                Log.Error(e, "Failed to receive a message.");
            }
            finally
            {
                if (streamer != null)
                {
                    streamer.Dispose();
                }
            }
        }

        private bool IsIncomingMessageIdValid(ulong messageId)
        {
            // TODO: check.
            return true;
        }

        private ulong GetNextMessageId()
        {
            return _messageIdGenerator.GetNextMessageId();
        }

        #region TL methods
        /// <summary>
        ///     Request pq.
        /// </summary>
        /// <returns>Response with pq.</returns>
        public async Task<IResPQ> ReqPqAsync(ReqPqArgs args)
        {
            return await SendUnencryptedMessageAndWaitForResponse<IResPQ>(args, DefaultRpcTimeout);
        }

        public async Task<IServerDHParams> ReqDHParamsAsync(ReqDHParamsArgs args)
        {
            return await SendUnencryptedMessageAndWaitForResponse<IServerDHParams>(args, DefaultRpcTimeout);
        }

        public async Task<ISetClientDHParamsAnswer> SetClientDHParamsAsync(SetClientDHParamsArgs args)
        {
            return await SendUnencryptedMessageAndWaitForResponse<ISetClientDHParamsAnswer>(args, DefaultRpcTimeout);
        }

        public async Task<IRpcDropAnswer> RpcDropAnswerAsync(RpcDropAnswerArgs args)
        {
            throw new NotImplementedException();
        }

        public Task<IFutureSalts> GetFutureSaltsAsync(GetFutureSaltsArgs args)
        {
            throw new NotImplementedException();
        }

        public Task<IPong> PingAsync(PingArgs args)
        {
            throw new NotImplementedException();
        }

        public Task<IPong> PingDelayDisconnectAsync(PingDelayDisconnectArgs args)
        {
            throw new NotImplementedException();
        }

        public Task<IDestroySessionRes> DestroySessionAsync(DestroySessionArgs args)
        {
            throw new NotImplementedException();
        }

        public Task HttpWaitAsync(HttpWaitArgs args)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Disposable
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;

            if (isDisposing)
            {
                Disconnect().Wait(5000);

                _outMessages.Dispose();
                _outMessages = null;

                _inMessages.Dispose();
                _inMessages = null;

                _inMessagesHistory.Dispose();
                _inMessagesHistory = null;

                _outMessagesHistory.Dispose();
                _outMessagesHistory = null;
            }
        }
        #endregion
    }
}
