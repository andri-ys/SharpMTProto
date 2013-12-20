// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnectionFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;
using BigMath.Utils;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SharpTL;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class MTProtoConnectionFacts
    {
        [Test]
        public async Task Should_send_and_receive_unencrypted_message()
        {
            byte[] messageData = Enumerable.Range(0, 255).Select(i => (byte) i).ToArray();

            var inStreamer = new TLStreamer().Syncronized();
            var outStreamer = new TLStreamer().Syncronized();

            var mockConnector = new Mock<IConnector>();
            mockConnector.SetupGet(connector => connector.InStream).Returns(inStreamer);
            mockConnector.SetupGet(connector => connector.OutStream).Returns(outStreamer);

            using (var connection = new MTProtoConnection(mockConnector.Object))
            {
                connection.Open();

                // Testing sending.
                mockConnector.VerifyGet(connector => connector.OutStream, Times.AtLeastOnce);

                UnencryptedMessage message = connection.SendUnencryptedMessage(messageData);
                await Task.Delay(100); // Wait while internal sender processes the message.
                
                byte[] expectedMessageBytes =
                    "0x0000000000000000".ToBytes().Concat(message.MessageId.ToBytes()).Concat("0xFF000000".ToBytes()).Concat(messageData).ToArray();
                
                outStreamer.Position = 0;
                byte[] actualMessageBytes = outStreamer.ReadBytes((int) outStreamer.Length);
                actualMessageBytes.Should().BeEquivalentTo(expectedMessageBytes);


                // Testing receiving.
                mockConnector.VerifyGet(connector => connector.InStream, Times.AtLeastOnce);

                inStreamer.Write(expectedMessageBytes, 0, expectedMessageBytes.Length);
                inStreamer.Position -= expectedMessageBytes.Length;
                await inStreamer.FlushAsync();
                await Task.Delay(100); // Wait while internal receiver processes the message.

                inStreamer.Position.Should().Be(expectedMessageBytes.Length);

                connection.Close();
            }

            outStreamer.Close();
            outStreamer.Close();
        }
    }
}
