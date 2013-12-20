// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnection.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel;
using Catel.Logging;
using SharpMTProto.Annotations;
using SharpMTProto.Utils;
using SharpTL;

namespace SharpMTProto
{
    /// <summary>
    ///     Default MTProto connection.
    /// </summary>
    public class MTProtoConnection : IMTProtoConnection
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly Queue<IMessage> _inboxMessages = new Queue<IMessage>();
        private readonly object _ingoingMessagesSyncRoot = new object();
        private readonly Queue<IMessage> _outboxMessages = new Queue<IMessage>();
        private readonly object _outgoingMessagesSyncRoot = new object();
        private readonly Stack<IMessage> _sentMessages = new Stack<IMessage>();
        private IConnector _connector;
        private CancellationToken _inOutCancellationToken;
        private CancellationTokenSource _inOutCts;
        private bool _isDisposed;
        private ulong _lastMessageId;
        private Task _receiverTask;
        private Task _senderTask;

        public MTProtoConnection([NotNull] IConnector connector)
        {
            Argument.IsNotNull(() => connector);

            _connector = connector;

            _inOutCts = new CancellationTokenSource();
            _inOutCancellationToken = _inOutCts.Token;
        }

        public UnencryptedMessage SendUnencryptedMessage(byte[] messageData)
        {
            var message = new UnencryptedMessage(GetNextMessageId(), messageData);
            lock (_outgoingMessagesSyncRoot)
            {
                _outboxMessages.Enqueue(message);
            }
            return message;
        }

        public byte[] ReceiveUnencryptedMessage()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            Dispose(true);
        }

        /// <summary>
        ///     Start sender and receiver tasks.
        /// </summary>
        public void Open()
        {
            StartSender();
            StartReceiver();
        }

        private void StartSender()
        {
            if (_senderTask != null && !_senderTask.IsCompleted)
            {
                Log.Info("Sender already started. Ignoring start request.");
                return;
            }

            if (_connector.OutStream == null)
            {
                Log.Info("Connector does NOT support out stream.");
                return;
            }

            Log.Info("Starting MTProto connection sender.");

            _senderTask = new Task(async () =>
            {
                Log.Info("Sender started. Waiting for messages in the outbox queue...");
                TLStreamer streamer = null;
                try
                {
                    streamer = new TLStreamer(_connector.OutStream, true);
                    while (!_inOutCancellationToken.IsCancellationRequested)
                    {
                        IMessage messageToSent = null;
                        lock (_outgoingMessagesSyncRoot)
                        {
                            if (_outboxMessages.Count > 0)
                            {
                                messageToSent = _outboxMessages.Peek();
                            }
                        }
                        if (messageToSent == null)
                        {
                            await Task.Delay(10, _inOutCancellationToken);
                            continue;
                        }
                        bool wasSent = false;
                        try
                        {
                            Log.Info("Sending message from the outbox queue...");

                            await streamer.WriteAsync(messageToSent.MessageBytes, 0, messageToSent.MessageBytes.Length, _inOutCancellationToken);

                            wasSent = true;
                            Log.Info(string.Format("Sent unencrypted message ({0} bytes): {1}", messageToSent.Length,
                                messageToSent.MessageBytes.ToHexaString(spaceEveryByte: true)));
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Failed to send a message.");
                        }
                        if (wasSent)
                        {
                            lock (_outgoingMessagesSyncRoot)
                            {
                                _outboxMessages.Dequeue();
                                _sentMessages.Push(messageToSent);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Info(string.Format("Sender task has canceled."));
                }
                catch (Exception e)
                {
                    Log.Error(e, "Sender corrupted.");
                }
                finally
                {
                    if (streamer != null)
                    {
                        streamer.Dispose();
                    }
                    Log.Info(string.Format("Sender stopped."));
                }
            }, _inOutCancellationToken, TaskCreationOptions.LongRunning);

            _senderTask.Start();
        }

        /// <summary>
        ///     Starts receiver.
        /// </summary>
        protected virtual void StartReceiver()
        {
            if (_receiverTask != null && !_receiverTask.IsCompleted)
            {
                Log.Info("Receiver already started. Ignoring start request.");
                return;
            }

            if (_connector.InStream == null)
            {
                Log.Info("Connector does NOT support in stream.");
                return;
            }

            Log.Info("Starting MTProto connection receiver...");

            _receiverTask = new Task(async () =>
            {
                Log.Info("Receiver started. Waiting for incoming message...");
                try
                {
                    using (var streamer = new TLStreamer(_connector.InStream, true))
                    {
                        while (!_inOutCancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                if (streamer.Position == streamer.Length)
                                {
                                    await Task.Delay(10, _inOutCancellationToken);
                                    continue;
                                }
                                ulong authKeyId = streamer.ReadULong();
                                Log.Info(string.Format("Received message with auth key ID = [0x{0:X16}].", authKeyId));
                                if (authKeyId == 0)
                                {
                                    // Assume the stream has an unencrypted message.
                                    Log.Info("Auth key ID is zero, hence assume this is an unencrypted message.");

                                    // Reading message ID.
                                    ulong messageId = streamer.ReadULong();
                                    if (!IsIncomingMessageIdValid(messageId))
                                    {
                                        throw new InvalidMessageException(string.Format("Message ID [0x{0:X16}] is invalid.", messageId));
                                    }

                                    // Reading message data length.
                                    int messageDataLength = streamer.ReadInt();
                                    if (messageDataLength == 0)
                                    {
                                        throw new InvalidMessageException("Message data length must be greater than zero.");
                                    }

                                    // Reading message data.
                                    var messageData = new byte[messageDataLength];
                                    int read = await streamer.ReadAsync(messageData, 0, messageDataLength, _inOutCancellationToken);
                                    if (read != messageDataLength)
                                    {
                                        throw new InvalidMessageException(string.Format("Actual message data length ({0}) is not as expected ({1}).", read,
                                            messageDataLength));
                                        // TODO: read message data if read is less than expected.
                                    }

                                    // Enqueuing  message to the inbox.
                                    var message = new UnencryptedMessage(messageId, messageData);
                                    lock (_ingoingMessagesSyncRoot)
                                    {
                                        _inboxMessages.Enqueue(message);
                                    }

                                    Log.Info(string.Format("Received unencrypted message ({0} bytes): {1}", message.Length,
                                        message.MessageBytes.ToHexaString(spaceEveryByte: true)));
                                }
                                else
                                {
                                    // Assume the stream has an encrypted message.
                                    Log.Info("Received encrypted message. (NOT supported yet).");
                                }
                            }
                            catch (InvalidMessageException e)
                            {
                                Log.Error(e, "Failed to receive a message.");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Log.Info(string.Format("Receiver task has canceled."));
                }
                catch (Exception e)
                {
                    Log.Error(e, "Receiver corrupted.");
                }
                finally
                {
                    Log.Info(string.Format("Receiver stopped."));
                }
            }, _inOutCancellationToken, TaskCreationOptions.LongRunning);

            _receiverTask.Start();
        }

        private bool IsIncomingMessageIdValid(ulong messageId)
        {
            // TODO: check.
            return true;
        }

        private ulong GetNextMessageId()
        {
            ulong messageId = UnixTimeUtils.GetCurrentUnixTimestampSeconds();
            messageId <<= 32;
            messageId -= messageId%4;
            if (messageId == _lastMessageId)
            {
                messageId += 4;
            }
            _lastMessageId = messageId;
            return messageId;
        }

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
                _inOutCts.Cancel();

                try
                {
                    if (_senderTask != null)
                    {
                        _senderTask.Wait();
                        _senderTask = null;
                    }
                    if (_receiverTask != null)
                    {
                        _receiverTask.Wait();
                        _receiverTask = null;
                    }
                }
                catch (AggregateException aex)
                {
                    foreach (Exception iex in aex.InnerExceptions)
                    {
                        if (iex is OperationCanceledException)
                        {
                            Log.Info(iex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while waiting for sender task cancellation.");
                }

                _inOutCts.Dispose();
                _inOutCts = null;

                // ReSharper disable once SuspiciousTypeConversion.Global
                var dCon = _connector as IDisposable;
                if (dCon != null)
                {
                    dCon.Dispose();
                }
                _connector = null;
            }
        }
        #endregion
    }
}
