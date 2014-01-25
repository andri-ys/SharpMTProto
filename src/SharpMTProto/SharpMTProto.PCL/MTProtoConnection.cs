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
        private static readonly Random Rnd = new Random();
        private readonly IEncryptionServices _encryptionServices;
        private readonly IHashServices _hashServices;
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly Subject<object> _responses = new Subject<object>();
        private readonly ulong _sessionId;
        private readonly TLRig _tlRig;
        private readonly ITransport _transport;
        private readonly ITransportFactory _transportFactory;
        private byte[] _authKey;
        private CancellationToken _connectionCancellationToken;
        private CancellationTokenSource _connectionCts;
        private Subject<IMessage> _inMessages = new Subject<IMessage>();
        private ReplaySubject<IMessage> _inMessagesHistory = new ReplaySubject<IMessage>(100);
        private bool _isDisposed;
        private uint _messageSeqNumber;
        private Subject<IMessage> _outMessages = new Subject<IMessage>();
        private ReplaySubject<IMessage> _outMessagesHistory = new ReplaySubject<IMessage>(100);
        private ulong _salt;
        private volatile MTProtoConnectionState _state = MTProtoConnectionState.Disconnected;

        public MTProtoConnection(TransportConfig transportConfig, [NotNull] ITransportFactory transportFactory, [NotNull] TLRig tlRig,
            [NotNull] IMessageIdGenerator messageIdGenerator, [NotNull] IHashServices hashServices, [NotNull] IEncryptionServices encryptionServices)
        {
            Argument.IsNotNull(() => transportFactory);
            Argument.IsNotNull(() => tlRig);
            Argument.IsNotNull(() => messageIdGenerator);
            Argument.IsNotNull(() => hashServices);
            Argument.IsNotNull(() => encryptionServices);

            _transportFactory = transportFactory;
            _tlRig = tlRig;
            _messageIdGenerator = messageIdGenerator;
            _hashServices = hashServices;
            _encryptionServices = encryptionServices;

            DefaultRpcTimeout = Defaults.RpcTimeout;
            DefaultConnectTimeout = Defaults.ConnectTimeout;

            _sessionId = GetNextSessionId();

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

            _inMessages.ObserveOn(DefaultScheduler.Instance).Subscribe(ProcessIncomingMessage);
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

        public void SendMessage(IMessage message)
        {
            ThrowIfDiconnected();

            _outMessages.OnNext(message);
        }

        public async Task<TResponse> SendMessage<TResponse>(object requestMessageDataObject, TimeSpan timeout, MessageType messageType) where TResponse : class
        {
            ThrowIfDiconnected();

            byte[] messageData = _tlRig.Serialize(requestMessageDataObject);

            Task<TResponse> resultTask = _responses.Select(o => o as TResponse).Where(r => r != null).FirstAsync().Timeout(timeout).ToTask(_connectionCancellationToken);

            switch (messageType)
            {
                case MessageType.Plain:
                    SendPlainMessage(messageData);
                    break;
                case MessageType.Encrypted:
                    SendEncryptedMessage(messageData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("messageType");
            }

            return await resultTask;
        }

        public void SendPlainMessage(byte[] messageData)
        {
            ThrowIfDiconnected();

            SendMessage(new PlainMessage(GetNextMessageId(), messageData));
        }

        /// <summary>
        ///     Sends plain (unencrypted) message and waits for a response.
        /// </summary>
        /// <typeparam name="TResponse">Type of the response which will be awaited.</typeparam>
        /// <param name="requestMessageDataObject">Request message data.</param>
        /// <param name="timeout">Timeout.</param>
        /// <returns>Response.</returns>
        /// <exception cref="TimeoutException">When response is not captured within a specified timeout.</exception>
        public async Task<TResponse> SendPlainMessage<TResponse>(object requestMessageDataObject, TimeSpan timeout) where TResponse : class
        {
            return await SendMessage<TResponse>(requestMessageDataObject, timeout, MessageType.Plain);
        }

        /// <summary>
        ///     Sends encrypted message.
        /// </summary>
        /// <param name="messageData">Message inner data.</param>
        /// <param name="isContentRelated">
        ///     Indicates wether message is content-related message requiring an explicit
        ///     acknowledgment.
        /// </param>
        public void SendEncryptedMessage(byte[] messageData, bool isContentRelated = true)
        {
            ThrowIfDiconnected();

            if (!IsEncryptionSupported)
            {
                throw new InvalidOperationException("Encryption is not supported. Setup encryption first by calling SetupEncryption() method.");
            }

            var message = new EncryptedMessage(_authKey, _salt, _sessionId, GetNextMessageId(), GetNextSeqNo(isContentRelated), messageData, Sender.Client, _hashServices,
                _encryptionServices);

            SendMessage(message);
        }

        public async Task<TResponse> SendEncryptedMessage<TResponse>(object requestMessageDataObject, TimeSpan timeout) where TResponse : class
        {
            return await SendMessage<TResponse>(requestMessageDataObject, timeout, MessageType.Encrypted);
        }

        public void SetupEncryption(byte[] authKey, ulong salt)
        {
            _authKey = authKey;
            _salt = salt;
        }

        public bool IsEncryptionSupported
        {
            get { return _authKey != null; }
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
                using (await _lock.LockAsync(CancellationToken.None))
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
            }, CancellationToken.None).ConfigureAwait(false);
        }

        private void ProcessIncomingMessage(IMessage message)
        {
            try
            {
                object response = _tlRig.Deserialize(message.MessageData);
                if (response != null)
                {
                    _responses.OnNext(response);
                }
            }
            catch (Exception e)
            {
                Log.Debug(e, "Error on message deserialization.");
            }
        }

        private void ThrowIfDiconnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not allowed when disconnected.");
            }
        }

        private uint GetNextSeqNo(bool isContentRelated)
        {
            uint x = (isContentRelated ? 1u : 0);
            uint result = _messageSeqNumber*2 + x;
            _messageSeqNumber += x;
            return result;
        }

        private static ulong GetNextSessionId()
        {
            return ((ulong) Rnd.Next()) << 32 + Rnd.Next();
        }

        private ulong GetNextMessageId()
        {
            return _messageIdGenerator.GetNextMessageId();
        }

        private static void LogMessageInOut(byte[] messageBytes, string inOrOut)
        {
            Log.Debug(string.Format("{0} ({1} bytes): {2}", inOrOut, messageBytes.Length, messageBytes.ToHexString()));
        }

        /// <summary>
        ///     Processes incoming message bytes.
        /// </summary>
        /// <param name="messageBytes">Incoming bytes.</param>
        private async void ProcessIncomingMessageBytes(byte[] messageBytes)
        {
            TLStreamer streamer = null;
            try
            {
                Log.Debug("Processing incoming message.");
                streamer = new TLStreamer(messageBytes);
                if (messageBytes.Length == 4)
                {
                    int error = streamer.ReadInt32();
                    Log.Debug("Received error code: {0}.", error);
                    return;
                }
                else if (messageBytes.Length < 20)
                {
                    throw new InvalidMessageException(
                        string.Format("Invalid message length: {0} bytes. Expected to be at least 20 bytes for message or 4 bytes for error code.", messageBytes.Length));
                }

                ulong authKeyId = streamer.ReadUInt64();
                if (authKeyId == 0)
                {
                    // Assume the message bytes has a plain (unencrypted) message.
                    Log.Debug(string.Format("Auth key ID = 0x{0:X16}. Assume this is a plain (unencrypted) message.", authKeyId));

                    // Reading message ID.
                    ulong messageId = streamer.ReadUInt64();
                    if (!IsIncomingMessageIdValid(messageId))
                    {
                        throw new InvalidMessageException(string.Format("Message ID = 0x{0:X16} is invalid.", messageId));
                    }

                    // Reading message data length.
                    int messageDataLength = streamer.ReadInt32();
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
                    var message = new PlainMessage(messageId, messageData);

                    Log.Debug(string.Format("Received plain message. Message ID = 0x{0:X16}. Message data length: {1} bytes.", messageId, messageDataLength));

                    _inMessages.OnNext(message);
                }
                else
                {
                    // Assume the stream has an encrypted message.
                    Log.Debug(string.Format("Auth key ID = 0x{0:X16}. Assume this is encrypted message.", authKeyId));
                    if (!IsEncryptionSupported)
                    {
                        Log.Debug("Encryption is not supported by this connection.");
                        return;
                    }

                    var message = new EncryptedMessage(_authKey, messageBytes, Sender.Server, _hashServices, _encryptionServices);

                    Log.Debug(string.Format("Received encrypted message. Message ID = 0x{0:X16}. Message data length: {1} bytes.", message.MessageId, message.MessageDataLength));

                    _inMessages.OnNext(message);
                }
            }
            catch (Exception e)
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

        #region TL methods
        /// <summary>
        ///     Request pq.
        /// </summary>
        /// <returns>Response with pq.</returns>
        public async Task<IResPQ> ReqPqAsync(ReqPqArgs args)
        {
            return await SendPlainMessage<IResPQ>(args, DefaultRpcTimeout);
        }

        public async Task<IServerDHParams> ReqDHParamsAsync(ReqDHParamsArgs args)
        {
            return await SendPlainMessage<IServerDHParams>(args, DefaultRpcTimeout);
        }

        public async Task<ISetClientDHParamsAnswer> SetClientDHParamsAsync(SetClientDHParamsArgs args)
        {
            return await SendPlainMessage<ISetClientDHParamsAnswer>(args, DefaultRpcTimeout);
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

    public enum MessageType
    {
        Plain,
        Encrypted
    }
}
