// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptedMessageFacts.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;
using BigMath;
using BigMath.Utils;
using Catel.IoC;
using FluentAssertions;
using NUnit.Framework;
using SharpMTProto.Services;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class EncryptedMessageFacts
    {
        [TestCase(Sender.Client)]
        [TestCase(Sender.Server)]
        public void Should_create(Sender sender)
        {
            byte[] authKey =
                "752BC8FC163832CB2606F7F3DC444D39A6D725761CA2FC984958E20EB7FDCE2AA1A65EB92D224CEC47EE8339AA44DF3906D79A01148CB6AACF70D53F98767EBD7EADA5A63C4229117EFBDB50DA4399C9E1A5D8B2550F263F3D43B936EF9259289647E7AAC8737C4E007C0C9108631E2B53C8900C372AD3CCA25E314FBD99AFFD1B5BCB29C5E40BB8366F1DFD07B053F1FBBBE0AA302EEEE5CF69C5A6EA7DEECDD965E0411E3F00FE112428330EBD432F228149FD2EC9B5775050F079C69CED280FE7E13B968783E3582B9C58CEAC2149039B3EF5A4265905D661879A41AF81098FBCA6D0B91D5B595E1E27E166867C155A3496CACA9FD6CF5D16DB2ADEBB2D3E"
                    .HexToBytes();

            const ulong expectedAuthKeyId = 0x1a0a7a922fcdae14;
            Int128 expectedMsgKey = Int128.Parse("0x3B7400E9316554B14686D405F0EAE4A8");

            var serviceLocator = new ServiceLocator();
            serviceLocator.RegisterType<IHashServices, HashServices>();
            serviceLocator.RegisterType<IEncryptionServices, EncryptionServices>();

            var hashServices = serviceLocator.ResolveType<IHashServices>();
            var encryptionServices = serviceLocator.ResolveType<IEncryptionServices>();

            byte[] messageData = Enumerable.Range(1, 100).ToArray().ConvertAll(i => (byte) i);
            const int expectedMessageLength = 168;

            var message = new EncryptedMessage(authKey, 1, 2, 3, 4, messageData, sender, hashServices, encryptionServices);
            message.AuthKeyId.Should().Be(expectedAuthKeyId);
            message.Length.Should().Be(expectedMessageLength);
            message.MessageData.ShouldAllBeEquivalentTo(messageData);
            message.MessageDataLength.Should().Be(messageData.Length);
            message.Salt.Should().Be(1);
            message.SessionId.Should().Be(2);
            message.MessageId.Should().Be(3);
            message.SeqNumber.Should().Be(4);
            message.MsgKey.Should().Be(expectedMsgKey);

            // Restoring an encrypted message.
            var decryptedMessage = new EncryptedMessage(authKey, message.MessageBytes, sender, hashServices, encryptionServices);
            decryptedMessage.AuthKeyId.Should().Be(expectedAuthKeyId);
            decryptedMessage.Length.Should().Be(expectedMessageLength);
            decryptedMessage.MessageData.ShouldAllBeEquivalentTo(messageData);
            decryptedMessage.MessageDataLength.Should().Be(messageData.Length);
            decryptedMessage.Salt.Should().Be(1);
            decryptedMessage.SessionId.Should().Be(2);
            decryptedMessage.MessageId.Should().Be(3);
            decryptedMessage.SeqNumber.Should().Be(4);
            decryptedMessage.MsgKey.Should().Be(expectedMsgKey);
        }
    }
}
