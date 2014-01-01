// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpTransport.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SharpMTProto.Transport;

namespace SharpMTProto.Extra
{
    /// <summary>
    ///     MTProto TCP transport.
    /// </summary>
    public class TcpTransport : ITransport
    {
        private const int MaxBufferSize = 2048;
        private static readonly ManualResetEvent ClientDone = new ManualResetEvent(false);
        private readonly TimeSpan _connectTimeout;

        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private Socket _socket;

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
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(byte[] value)
        {
            throw new NotImplementedException();
        }

        public IDisposable Subscribe(IObserver<byte[]> observer)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool IsConnected
        {
            get { return State == TransportState.Connected; }
        }

        public TransportState State { get; private set; }

        public async Task Connect()
        {
            await Connect(CancellationToken.None);
        }

        public async Task Connect(CancellationToken token)
        {
            _socket = new Socket(_ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            var args = new SocketAsyncEventArgs {RemoteEndPoint = new IPEndPoint(_ipAddress, _port)};
            args.Completed += (sender, eventArgs) =>
            {
                switch (eventArgs.SocketError)
                {
                    case SocketError.Success:
                    case SocketError.IsConnected:
                        State = TransportState.Connected;
                        break;
                    default:
                        State = TransportState.Disconnected;
                        break;
                }

                ClientDone.Set();
            };

            ClientDone.Reset();

            _socket.ConnectAsync(args);
            
            ClientDone.WaitOne(_connectTimeout);
        }

        public async Task Disconnect()
        {
            await Disconnect(CancellationToken.None);
        }

        public Task Disconnect(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        private async Task ReadAsync()
        {
            // Reusable SocketAsyncEventArgs and awaitable wrapper 
            var args = new SocketAsyncEventArgs();
            args.SetBuffer(new byte[0x1000], 0, 0x1000);
            var awaitable = new SocketAwaitable(args);

            // Do processing, continually receiving from the socket 
            while (true)
            {
                await _socket.ReceiveAsync(awaitable);
                if (args.SocketError != SocketError.IsConnected)
                {
                    State = TransportState.Disconnected;
                    break;
                }
                int bytesRead = args.BytesTransferred;
                if (bytesRead <= 0)
                {
                    break;
                }

                Console.WriteLine(bytesRead);
            }
        }
    }
}
