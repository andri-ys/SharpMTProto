// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnection.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Catel;
using Catel.Logging;
using SharpMTProto.Annotations;
using SharpMTProto.Utils;

namespace SharpMTProto
{
    /// <summary>
    ///     Default MTProto connection.
    /// </summary>
    public class MTProtoConnection : IMTProtoConnection
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly Queue<IMessage> _outboxMessages = new Queue<IMessage>();

        private readonly object _outgoingMessagesSyncRoot = new object();

        private readonly Stack<IMessage> _sentMessages = new Stack<IMessage>();
        private IConnector _connector;
        private bool _isDisposed;
        private ulong _lastMessageId;
        private CancellationToken _senderCancellationToken;
        private CancellationTokenSource _senderCts;
        private Task _senderTask;

        public MTProtoConnection([NotNull] IConnector connector)
        {
            Argument.IsNotNull(() => connector);

            _connector = connector;

            _senderCts = new CancellationTokenSource();
            _senderCancellationToken = _senderCts.Token;

            StartSender();
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

        private void StartSender()
        {
            Log.Info("Starting MTProto connection sender.");

            if (_senderTask != null && !_senderTask.IsCompleted)
            {
                Log.Info("Sender al");
                return;
            }

            _senderTask = new Task(async () =>
            {
                while (!_senderCancellationToken.IsCancellationRequested)
                {
                    await Task.Yield();
                    IMessage messageToSent;
                    lock (_outgoingMessagesSyncRoot)
                    {
                        if (_outboxMessages.Count == 0)
                        {
                            continue;
                        }
                        messageToSent = _outboxMessages.Peek();
                    }
                    bool wasSent = false;
                    try
                    {
                        _connector.SendData(messageToSent.MessageBytes);
                        wasSent = true;
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
            }, _senderCancellationToken, TaskCreationOptions.LongRunning);

            _senderTask.Start();
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
                _senderCts.Cancel();

                try
                {
                    _senderTask.Wait(1000);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error while waiting for sender task cancellation.");
                }

                _senderCts.Dispose();
                _senderCts = null;

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
