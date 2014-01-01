// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpTransportFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using SharpMTProto.Extra;
using SharpMTProto.Transport;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class TcpTransportFacts
    {
        [Test]
        public async Task Should_connect_and_disconnect()
        {
            var config = new TcpTransportConfig("127.0.0.1", 17313);
            var transport = new TcpTransport(config);

            transport.State.Should().Be(TransportState.Disconnected);
            transport.IsConnected.Should().BeFalse();

            await transport.Connect();

            transport.State.Should().Be(TransportState.Connected);
            transport.IsConnected.Should().BeTrue();

            await transport.Disconnect();

            transport.State.Should().Be(TransportState.Disconnected);
            transport.IsConnected.Should().BeFalse();
        }
    }
}
