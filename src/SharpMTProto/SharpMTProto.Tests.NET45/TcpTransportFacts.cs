// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpTransportFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel.Logging;
using FluentAssertions;
using Nito.AsyncEx;
using NUnit.Framework;
using SharpMTProto.Transport;
using SharpMTProto.Utils;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class TcpTransportFacts
    {
        [SetUp]
        public void SetUp()
        {
            LogManager.AddDebugListener(true);

            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            _serverSocket.Listen(1);
            _serverEndPoint = _serverSocket.LocalEndPoint as IPEndPoint;
        }

        [TearDown]
        public void TearDown()
        {
            if (_serverSocket != null)
            {
                _serverSocket.Close();
                _serverSocket = null;
            }
            _serverEndPoint = null;
        }

        private Socket _serverSocket;
        private IPEndPoint _serverEndPoint;

        private TcpTransport CreateTcpTransport()
        {
            var config = new TcpTransportConfig(_serverEndPoint.Address.ToString(), _serverEndPoint.Port) {MaxBufferSize = 0xFF};
            var transport = new TcpTransport(config);
            return transport;
        }

        [Test]
        public async Task Should_connect_and_disconnect()
        {
            TcpTransport transport = CreateTcpTransport();

            transport.State.Should().Be(TransportState.Disconnected);
            transport.IsConnected.Should().BeFalse();

            await transport.Connect();

            Socket clientSocket = _serverSocket.Accept();

            clientSocket.Should().NotBeNull();
            clientSocket.IsConnected().Should().BeTrue();

            transport.State.Should().Be(TransportState.Connected);
            transport.IsConnected.Should().BeTrue();

            await transport.Disconnect();
            transport.Dispose();

            clientSocket.IsConnected().Should().BeFalse();

            transport.State.Should().Be(TransportState.Disconnected);
            transport.IsConnected.Should().BeFalse();
        }

        [Test]
        public async Task Should_process_lost_connection()
        {
            TcpTransport transport = CreateTcpTransport();

            transport.State.Should().Be(TransportState.Disconnected);

            await transport.Connect();

            Socket clientSocket = _serverSocket.Accept();

            clientSocket.Should().NotBeNull();
            clientSocket.IsConnected().Should().BeTrue();

            transport.State.Should().Be(TransportState.Connected);

            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Disconnect(false);
            clientSocket.Close();

            await Task.Delay(200);

            transport.State.Should().Be(TransportState.Disconnected);

            await transport.Disconnect();

            transport.State.Should().Be(TransportState.Disconnected);
        }

        [Test]
        public async Task Should_receive()
        {
            TcpTransport transport = CreateTcpTransport();

            Task<byte[]> receiveTask = transport.FirstAsync().Timeout(TimeSpan.FromMilliseconds(1000)).ToTask();

            await transport.Connect();
            Socket clientSocket = _serverSocket.Accept();

            byte[] payload = "010203040506070809".HexToBytes();

            var packet = new TcpTransportPacket(0x0FA0B1C2, payload);
            clientSocket.Send(packet.Data);

            byte[] receivedData = await receiveTask;
            receivedData.Should().BeEquivalentTo(payload);

            await transport.Disconnect();
            clientSocket.Close();
        }

        [Test]
        public async Task Should_receive_big_payload()
        {
            TcpTransport transport = CreateTcpTransport();

            Task<byte[]> receiveTask = transport.FirstAsync().Timeout(TimeSpan.FromMilliseconds(1000)).ToTask();

            await transport.Connect();
            Socket clientSocket = _serverSocket.Accept();

            byte[] payload = Enumerable.Range(0, 255).Select(i => (byte) i).ToArray();

            var packet = new TcpTransportPacket(0x0FA0B1C2, payload);
            clientSocket.Send(packet.Data);

            byte[] receivedData = await receiveTask;
            receivedData.Should().BeEquivalentTo(payload);

            await transport.Disconnect();
            clientSocket.Close();
        }

        [Test]
        public async Task Should_receive_multiple_packets()
        {
            TcpTransport transport = CreateTcpTransport();

            var receivedMessages = new AsyncProducerConsumerQueue<byte[]>();
            transport.Subscribe(receivedMessages.Enqueue);

            await transport.Connect();
            Socket clientSocket = _serverSocket.Accept();

            byte[] payload1 = Enumerable.Range(0, 10).Select(i => (byte) i).ToArray();
            var packet1 = new TcpTransportPacket(0, payload1);

            byte[] payload2 = Enumerable.Range(11, 40).Select(i => (byte) i).ToArray();
            var packet2 = new TcpTransportPacket(1, payload2);

            byte[] payload3 = Enumerable.Range(51, 205).Select(i => (byte) i).ToArray();
            var packet3 = new TcpTransportPacket(2, payload3);

            byte[] allData = ArrayUtils.Combine(packet1.Data, packet2.Data, packet3.Data);

            byte[] dataPart1;
            byte[] dataPart2;
            allData.Split(50, out dataPart1, out dataPart2);

            clientSocket.Send(dataPart1);
            await Task.Delay(100);
            clientSocket.Send(dataPart2);

            await Task.Delay(1000);

            byte[] receivedData1 = await receivedMessages.DequeueAsync(CancellationTokenHelpers.Timeout(1000).Token);
            receivedData1.Should().BeEquivalentTo(payload1);

            byte[] receivedData2 = await receivedMessages.DequeueAsync(CancellationTokenHelpers.Timeout(1000).Token);
            receivedData2.Should().BeEquivalentTo(payload2);

            byte[] receivedData3 = await receivedMessages.DequeueAsync(CancellationTokenHelpers.Timeout(1000).Token);
            receivedData3.Should().BeEquivalentTo(payload3);

            await transport.Disconnect();
            clientSocket.Close();
        }
    }
}
