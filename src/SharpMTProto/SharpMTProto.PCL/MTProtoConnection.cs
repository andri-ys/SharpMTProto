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

        private readonly IConnectorFactory _connectorFactory;
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly IMessageIdGenerator _messageIdGenerator;
        private readonly TLRig _tlRig;
        private CancellationToken _connectionCancellationToken;

        private CancellationTokenSource _connectionCts;
        private IConnector _connector;

        private bool _isDisposed;

        private volatile MTProtoConnectionState _state = MTProtoConnectionState.Disconnected;

        #region Message hubs.
        private Subject<IMessage> _inMessages = new Subject<IMessage>();
        private ReplaySubject<IMessage> _inMessagesHistory = new ReplaySubject<IMessage>(100);
        private Subject<IMessage> _outMessages = new Subject<IMessage>();
        private ReplaySubject<IMessage> _outMessagesHistory = new ReplaySubject<IMessage>(100);
        #endregion

        public MTProtoConnection([NotNull] IConnectorFactory connectorFactory, [NotNull] TLRig tlRig) : this(connectorFactory, tlRig, new MessageIdGenerator())
        {
        }

        public MTProtoConnection([NotNull] IConnectorFactory connectorFactory, [NotNull] TLRig tlRig, [NotNull] IMessageIdGenerator messageIdGenerator)
        {
            Argument.IsNotNull(() => connectorFactory);
            Argument.IsNotNull(() => tlRig);
            Argument.IsNotNull(() => messageIdGenerator);

            _connectorFactory = connectorFactory;
            _tlRig = tlRig;
            _messageIdGenerator = messageIdGenerator;

            DefaultRpcTimeout = TimeSpan.FromSeconds(5);
            DefaultConnectTimeout = TimeSpan.FromSeconds(5);
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
            var result = MTProtoConnectResult.Other;

            await Task.Run(async () =>
            {
                using (await _lock.LockAsync(_connectionCancellationToken))
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
                        Log.Info("Connecting...");

                        _connectionCts = new CancellationTokenSource();
                        _connectionCancellationToken = _connectionCts.Token;

                        _connector = _connectorFactory.CreateConnector();

                        // History of messages in/out.
                        _inMessages.ObserveOn(DefaultScheduler.Instance).Subscribe(_inMessagesHistory, _connectionCancellationToken);
                        _outMessages.ObserveOn(DefaultScheduler.Instance).Subscribe(_outMessagesHistory, _connectionCancellationToken);

                        // Connector in/out.
                        _connector.ObserveOn(DefaultScheduler.Instance)
                            .Do(bytes => LogMessageInOut(bytes, "IN"))
                            .Subscribe(ProcessIncomingMessageBytes, _connectionCancellationToken);
                        _outMessages.ObserveOn(DefaultScheduler.Instance)
                            .Do(message => LogMessageInOut(message.MessageBytes, "OUT"))
                            .Subscribe(message => _connector.OnNext(message.MessageBytes), _connectionCancellationToken);

                        // TODO: add retry logic.
                        await _connector.Connect(DefaultConnectTimeout, _connectionCancellationToken);

                        Log.Info("Connected.");
                        result = MTProtoConnectResult.Success;
                    }
                    catch (TimeoutException e)
                    {
                        result = MTProtoConnectResult.Timeout;
                        Log.Error(e, "Failed to connect due to timeout.");
                    }
                    catch (Exception e)
                    {
                        result = MTProtoConnectResult.Other;
                        Log.Error(e, "Failed to connect.");
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
            }, _connectionCancellationToken).ConfigureAwait(false);

            return result;
        }

        public async Task Disconnect()
        {
            if (_connectionCts != null)
            {
                _connectionCts.Cancel();
                _connectionCts.Dispose();
                _connectionCts = null;
            }

            // ReSharper disable MethodSupportsCancellation
            await Task.Run(async () =>
            {
                using (await _lock.LockAsync())
                {
                    if (_state == MTProtoConnectionState.Disconnected)
                    {
                        return;
                    }

                    if (_connector != null)
                    {
                        _connector.Dispose();
                    }
                    _connector = null;

                    _state = MTProtoConnectionState.Disconnected;
                }
            }).ConfigureAwait(false);
            // ReSharper restore MethodSupportsCancellation
        }

        private static void LogMessageInOut(byte[] messageBytes, string inOrOut)
        {
            Log.Info(string.Format("{0} ({1} bytes): {2}", inOrOut, messageBytes.Length, messageBytes.ToHexaString(spaceEveryByte: true)));
        }

        /// <summary>
        ///     Processes incoming message bytes.
        /// </summary>
        /// <param name="bytes">Incoming bytes.</param>
        private async void ProcessIncomingMessageBytes(byte[] bytes)
        {
            // TODO: process bytes as raw TCP stream, but not as each portion of bytes is a separate message.
            TLStreamer streamer = null;
            try
            {
                Log.Info("Processing incoming message.");
                streamer = new TLStreamer(bytes);
                ulong authKeyId = streamer.ReadULong();
                Log.Info(string.Format("Auth key ID [0x{0:X16}].", authKeyId));
                if (authKeyId == 0)
                {
                    // Assume the message bytes has an unencrypted message.
                    Log.Info(string.Format("Assume this is unencrypted message."));

                    // Reading message ID.
                    ulong messageId = streamer.ReadULong();
                    if (!IsIncomingMessageIdValid(messageId))
                    {
                        throw new InvalidMessageException(string.Format("Message ID: [0x{0:X16}] is invalid.", messageId));
                    }

                    // Reading message data length.
                    int messageDataLength = streamer.ReadInt();
                    if (messageDataLength == 0)
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

                    Log.Info(string.Format("Received unencrypted message. Message ID: [0x{0:X16}]. Message data length: {1} bytes.", messageId, messageDataLength));

                    _inMessages.OnNext(message);
                }
                else
                {
                    // Assume the stream has an encrypted message.
                    Log.Info(string.Format("Auth key ID [0x{0:X16}]. Assume this is encrypted message. (Encrypted messages NOT supported yet. Skipping.)", authKeyId));
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
            throw new NotImplementedException();
        }

        public async Task<ISetClientDHParamsAnswer> SetClientDHParamsAsync(SetClientDHParamsArgs args)
        {
            throw new NotImplementedException();
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
