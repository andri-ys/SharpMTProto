// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PlainMessageFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;
using BigMath.Utils;
using FluentAssertions;
using NUnit.Framework;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class PlainMessageFacts
    {
        [Test]
        public void Should_create()
        {
            const ulong messageId = 0x0102030405060708UL;
            byte[] messageData = Enumerable.Range(0, 255).Select(i => (byte) i).ToArray();
            byte[] messageBytes = ("0x0000000000000000" + "0807060504030201" + "000000FF").HexToBytes().Concat(messageData).ToArray();

            var message = new PlainMessage(messageId, messageData);

            message.Should().NotBeNull();
            message.Length.Should().Be(messageBytes.Length);
            message.DataLength.Should().Be(messageData.Length);
            message.GetMessageData().Should().BeEquivalentTo(messageData);
            message.MessageBytes.Should().BeEquivalentTo(messageBytes);
        }
    }
}
