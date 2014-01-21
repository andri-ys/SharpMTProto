// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpTransport.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel.Logging;
using Nito.AsyncEx;
using SharpMTProto.Utils;
using SharpTL;

namespace SharpMTProto.Transport
{
    /// <summary>
    ///     MTProto TCP transport.
    /// </summary>
    public class TcpTransport : ITransport
    {
        private const int PacketLengthBytesCount = 4;
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly TimeSpan _connectTimeout;
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly byte[] _readerBuffer;

        private readonly AsyncLock _stateAsyncLock = new AsyncLock();
        private readonly byte[] _tempLengthBuffer = new byte[PacketLengthBytesCount];

        private CancellationTokenSource _connectionCancellationTokenSource;
        private Subject<byte[]> _in = new Subject<byte[]>();
        private int _nextPacketBytesCountLeft;
        private byte[] _nextPacketDataBuffer;
        private TLStreamer _nextPacketStreamer;
        private Task _receiverTask;
        private Socket _socket;
        private volatile TransportState _state = TransportState.Disconnected;
        private int _tempLengthBufferFill;
        private int _packetNumber;

        public TcpTransport(TcpTransportConfig config)
        {
            if (config.Port <= 0 || config.Port > ushort.MaxValue)
            {
                throw new ArgumentException("Port is incorrect.");
            }

            IPAddress ipAddress;
            if (!IPAddress.TryParse(config.IPAddress, out ipAddress))
            {
                throw new ArgumentException("IP address is incorrect.");
            }

            _port = config.Port;
            _ipAddress = ipAddress;
            _connectTimeout = config.ConnectTimeout;

            _readerBuffer = new byte[config.MaxBufferSize];

            _socket = new Socket(_ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public IDisposable Subscribe(IObserver<byte[]> observer)
        {
            return _in.Subscribe(observer);
        }

        public bool IsConnected
        {
            get { return State == TransportState.Connected; }
        }

        public TransportState State
        {
            get { return _state; }
        }

        public void Connect()
        {
            ConnectAsync().Wait();
        }

        public async Task ConnectAsync()
        {
            await ConnectAsync(CancellationToken.None);
        }

        public async Task ConnectAsync(CancellationToken token)
        {
            using (await _stateAsyncLock.LockAsync(token))
            {
                if (State == TransportState.Connected)
                {
                    return;
                }

                var args = new SocketAsyncEventArgs {RemoteEndPoint = new IPEndPoint(_ipAddress, _port)};
                
                var awaitable = new SocketAwaitable(args);

                try
                {
                    _packetNumber = 0;
                    await _socket.ConnectAsync(awaitable);
                }
                catch (SocketException e)
                {
                    Log.Debug(e);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    _state = TransportState.Disconnected;
                    throw;
                }

                switch (args.SocketError)
                {
                    case SocketError.Success:
                    case SocketError.IsConnected:
                        _state = TransportState.Connected;
                        break;
                    default:
                        _state = TransportState.Disconnected;
                        break;
                }
                if (_state != TransportState.Connected)
                {
                    return;
                }
                _connectionCancellationTokenSource = new CancellationTokenSource();
                _receiverTask = StartReceiver(_connectionCancellationTokenSource.Token);
            }
        }

        public void Disconnect()
        {
            DisconnectAsync().Wait();
        }

        public async Task DisconnectAsync()
        {
            await DisconnectAsync(CancellationToken.None);
        }

        public async Task DisconnectAsync(CancellationToken token)
        {
            using (await _stateAsyncLock.LockAsync(token))
            {
                if (_state == TransportState.Disconnected)
                {
                    return;
                }
                var args = new SocketAsyncEventArgs();
                var awaitable = new SocketAwaitable(args);
                try
                {
                    if (_socket.Connected)
                    {
                        _socket.Shutdown(SocketShutdown.Both);
                        await _socket.DisconnectAsync(awaitable);
                    }
                }
                catch (SocketException e)
                {
                    Log.Debug(e);
                }

                _state = TransportState.Disconnected;
            }
        }

        public void Send(byte[] payload)
        {
            SendAsync(payload).Wait();
        }

        public Task SendAsync(byte[] payload)
        {
            return SendAsync(payload, CancellationToken.None);
        }

        public async Task SendAsync(byte[] payload, CancellationToken token)
        {
            await Task.Run(async () =>
            {
                var packet = new TcpTransportPacket(_packetNumber++, payload);

                var args = new SocketAsyncEventArgs();
                args.SetBuffer(packet.Data, 0, packet.Data.Length);

                var awaitable = new SocketAwaitable(args);
                await _socket.SendAsync(awaitable);
            }, token).ConfigureAwait(false);
        }

        private async Task StartReceiver(CancellationToken token)
        {
            await Task.Run(async () =>
            {
                var args = new SocketAsyncEventArgs();
                args.SetBuffer(_readerBuffer, 0, _readerBuffer.Length);
                var awaitable = new SocketAwaitable(args);

                while (!token.IsCancellationRequested && _socket.IsConnected())
                {
                    try
                    {
                        if (_socket.Available == 0)
                        {
                            await Task.Delay(100, token);
                            continue;
                        }
                        await _socket.ReceiveAsync(awaitable);
                    }
                    catch (SocketException e)
                    {
                        Log.Debug(e);
                    }
                    if (args.SocketError != SocketError.Success)
                    {
                        break;
                    }
                    int bytesRead = args.BytesTransferred;
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    try
                    {
                        await ProcessReceivedData(new ArraySegment<byte>(_readerBuffer, 0, bytesRead));
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Critical error while precessing received data.");
                        break;
                    }
                }
                await DisconnectAsync(CancellationToken.None);
            }, token).ConfigureAwait(false);
        }

        private async Task ProcessReceivedData(ArraySegment<byte> buffer)
        {
            try
            {
                int bytesRead = 0;
                while (bytesRead < buffer.Count)
                {
                    int startIndex = buffer.Offset + bytesRead;
                    int bytesToRead = buffer.Count - bytesRead;

                    if (_nextPacketBytesCountLeft == 0)
                    {
                        int tempLengthBytesToRead = PacketLengthBytesCount - _tempLengthBufferFill;
                        tempLengthBytesToRead = (bytesToRead < tempLengthBytesToRead) ? bytesToRead : tempLengthBytesToRead;
                        Buffer.BlockCopy(buffer.Array, startIndex, _tempLengthBuffer, _tempLengthBufferFill, tempLengthBytesToRead);

                        _tempLengthBufferFill += tempLengthBytesToRead;
                        if (_tempLengthBufferFill < PacketLengthBytesCount)
                        {
                            break;
                        }

                        startIndex += tempLengthBytesToRead;
                        bytesToRead -= tempLengthBytesToRead;

                        _tempLengthBufferFill = 0;
                        _nextPacketBytesCountLeft = _tempLengthBuffer.ToInt32();

                        if (_nextPacketDataBuffer == null || _nextPacketDataBuffer.Length < _nextPacketBytesCountLeft || _nextPacketStreamer == null)
                        {
                            _nextPacketDataBuffer = new byte[_nextPacketBytesCountLeft];
                            _nextPacketStreamer = new TLStreamer(_nextPacketDataBuffer);
                        }

                        // Writing packet length.
                        _nextPacketStreamer.Write(_tempLengthBuffer);
                        _nextPacketBytesCountLeft -= PacketLengthBytesCount;
                        bytesRead += PacketLengthBytesCount;
                    }

                    bytesToRead = bytesToRead > _nextPacketBytesCountLeft ? _nextPacketBytesCountLeft : bytesToRead;

                    _nextPacketStreamer.Write(buffer.Array, startIndex, bytesToRead);

                    bytesRead += bytesToRead;
                    _nextPacketBytesCountLeft -= bytesToRead;

                    if (_nextPacketBytesCountLeft > 0)
                    {
                        break;
                    }

                    var packet = new TcpTransportPacket(_nextPacketDataBuffer, 0, (int) _nextPacketStreamer.Position);

                    await ProcessReceivedPacket(packet);

                    _nextPacketBytesCountLeft = 0;
                    _nextPacketStreamer.Position = 0;
                }
            }
            catch (Exception)
            {
                if (_nextPacketStreamer != null)
                {
                    _nextPacketStreamer.Dispose();
                    _nextPacketStreamer = null;
                }
                _nextPacketDataBuffer = null;
                _nextPacketBytesCountLeft = 0;

                throw;
            }
        }

        private async Task ProcessReceivedPacket(TcpTransportPacket packet)
        {
            await Task.Run(() => _in.OnNext(packet.GetPayloadCopy()));
        }

        #region Disposing
        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool isDisposing)
        {
            if (!isDisposing)
            {
                return;
            }
            if (_connectionCancellationTokenSource != null)
            {
                _connectionCancellationTokenSource.Cancel();
                _connectionCancellationTokenSource = null;
            }
            if (_receiverTask != null)
            {
                _receiverTask.Dispose();
                _receiverTask = null;
            }
            if (_nextPacketStreamer != null)
            {
                _nextPacketStreamer.Dispose();
                _nextPacketStreamer = null;
            }
            if (_in != null)
            {
                _in.Dispose();
                _in = null;
            }
            if (_socket != null)
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _socket.Disconnect(false);
                    _socket.Close();
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                finally
                {
                    _socket = null;
                }
            }
        }
        #endregion
    }
}
