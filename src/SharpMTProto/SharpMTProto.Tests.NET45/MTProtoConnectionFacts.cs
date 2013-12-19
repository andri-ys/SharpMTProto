// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnectionFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;
using BigMath.Utils;
using Moq;
using NUnit.Framework;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class MTProtoConnectionFacts
    {
        [Test]
        public async Task Should_send_unencrypted_message()
        {
            byte[] messageData = Enumerable.Range(0, 255).Select(i => (byte) i).ToArray();

            var mockConnector = new Mock<IConnector>();

            UnencryptedMessage message;

            using (var connection = new MTProtoConnection(mockConnector.Object))
            {
                message = connection.SendUnencryptedMessage(messageData);
                await Task.Delay(100); // Wait while internal sender thread process the message.
            }

            byte[] messageBytes =
                "0x0000000000000000".ToByteArray().Concat(message.MessageId.ToBytes(true)).Concat("0xFF000000".ToByteArray()).Concat(messageData).ToArray();

            mockConnector.Verify(connector => connector.SendData(It.Is<byte[]>(bytes => bytes.SequenceEqual(messageBytes))), Times.Exactly(1));
        }
    }
}
