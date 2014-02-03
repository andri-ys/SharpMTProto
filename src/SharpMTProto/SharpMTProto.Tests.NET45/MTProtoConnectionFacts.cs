// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnectionFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel.IoC;
using Catel.Logging;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SharpMTProto.Services;
using SharpMTProto.Transport;
using SharpTL;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class MTProtoConnectionFacts
    {
        [SetUp]
        public void SetUp()
        {
            LogManager.AddDebugListener(true);
        }

        [Test]
        public async Task Should_send_and_receive_plain_message()
        {
            IServiceLocator serviceLocator = new ServiceLocator();

            byte[] messageData = Enumerable.Range(0, 255).Select(i => (byte) i).ToArray();
            byte[] expectedMessageBytes = "00000000000000000807060504030201FF000000".HexToBytes().Concat(messageData).ToArray();

            var inConnector = new Subject<byte[]>();

            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(connector => connector.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);
            serviceLocator.RegisterInstance(Mock.Of<TransportConfig>());
            serviceLocator.RegisterInstance(TLRig.Default);
            serviceLocator.RegisterInstance<IMessageIdGenerator>(new TestMessageIdsGenerator());
            serviceLocator.RegisterType<IHashServices, HashServices>();
            serviceLocator.RegisterType<IEncryptionServices, EncryptionServices>();
            serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>(RegistrationType.Transient);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                await connection.Connect();

                // Testing sending.
                var message = new PlainMessage(0x0102030405060708UL, messageData);
                connection.SendMessage(message);

                await Task.Delay(100); // Wait while internal sender processes the message.
                mockTransport.Verify(connector => connector.Send(expectedMessageBytes), Times.Once);

                // Testing receiving.
                mockTransport.Verify(connector => connector.Subscribe(It.IsAny<IObserver<byte[]>>()), Times.AtLeastOnce());

                inConnector.OnNext(expectedMessageBytes);

                await Task.Delay(100); // Wait while internal receiver processes the message.
                IMessage actualMessage = await connection.InMessagesHistory.FirstAsync().ToTask();
                actualMessage.MessageBytes.ShouldAllBeEquivalentTo(expectedMessageBytes);

                await connection.Disconnect();
            }
        }

        [Test]
        public async Task Should_send_plain_message_and_wait_for_response()
        {
            IServiceLocator serviceLocator = new ServiceLocator();

            var request = new TestRequest {TestId = 9};
            var expectedResponse = new TestResponse {TestId = 9, TestText = "Number 1"};
            var expectedResponseMessage = new PlainMessage(0x0102030405060708, TLRig.Default.Serialize(expectedResponse));

            var inConnector = new Subject<byte[]>();

            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(transport => transport.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));
            mockTransport.Setup(transport => transport.Send(It.IsAny<byte[]>())).Callback(() => inConnector.OnNext(expectedResponseMessage.MessageBytes));

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);
            serviceLocator.RegisterInstance(Mock.Of<TransportConfig>());
            serviceLocator.RegisterInstance(TLRig.Default);
            serviceLocator.RegisterInstance<IMessageIdGenerator>(new TestMessageIdsGenerator());
            serviceLocator.RegisterType<IHashServices, HashServices>();
            serviceLocator.RegisterType<IEncryptionServices, EncryptionServices>();
            serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>(RegistrationType.Transient);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                await connection.Connect();

                // Testing sending a plain message.
                TestResponse response = await connection.SendPlainMessage<TestResponse>(request, TimeSpan.FromSeconds(5));
                response.Should().NotBeNull();
                response.ShouldBeEquivalentTo(expectedResponse);

                await Task.Delay(100); // Wait while internal sender processes the message.
                IMessage inMessageTask = await connection.OutMessagesHistory.FirstAsync().ToTask();
                mockTransport.Verify(transport => transport.Send(inMessageTask.MessageBytes), Times.Once);

                await connection.Disconnect();
            }
        }

        [Test]
        public async Task Should_send_encrypted_message_and_wait_for_response()
        {
            byte[] authKey =
                "752BC8FC163832CB2606F7F3DC444D39A6D725761CA2FC984958E20EB7FDCE2AA1A65EB92D224CEC47EE8339AA44DF3906D79A01148CB6AACF70D53F98767EBD7EADA5A63C4229117EFBDB50DA4399C9E1A5D8B2550F263F3D43B936EF9259289647E7AAC8737C4E007C0C9108631E2B53C8900C372AD3CCA25E314FBD99AFFD1B5BCB29C5E40BB8366F1DFD07B053F1FBBBE0AA302EEEE5CF69C5A6EA7DEECDD965E0411E3F00FE112428330EBD432F228149FD2EC9B5775050F079C69CED280FE7E13B968783E3582B9C58CEAC2149039B3EF5A4265905D661879A41AF81098FBCA6D0B91D5B595E1E27E166867C155A3496CACA9FD6CF5D16DB2ADEBB2D3E"
                    .HexToBytes();

            ulong salt = 100500;

            IServiceLocator serviceLocator = new ServiceLocator();
            serviceLocator.RegisterType<IHashServices, HashServices>();
            serviceLocator.RegisterType<IEncryptionServices, EncryptionServices>();

            var request = new TestRequest { TestId = 9 };
            var expectedResponse = new TestResponse { TestId = 9, TestText = "Number 1" };
            var expectedResponseMessage = new EncryptedMessage(authKey, salt, 2, 0x0102030405060708, 3, TLRig.Default.Serialize(expectedResponse), Sender.Server,
                serviceLocator.ResolveType<IHashServices>(), serviceLocator.ResolveType<IEncryptionServices>());

            var inConnector = new Subject<byte[]>();

            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(transport => transport.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));
            mockTransport.Setup(transport => transport.Send(It.IsAny<byte[]>())).Callback(() => inConnector.OnNext(expectedResponseMessage.MessageBytes));

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);
            serviceLocator.RegisterInstance(Mock.Of<TransportConfig>());
            serviceLocator.RegisterInstance(TLRig.Default);
            serviceLocator.RegisterInstance<IMessageIdGenerator>(new TestMessageIdsGenerator());
            serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>(RegistrationType.Transient);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                connection.SetupEncryption(authKey, salt);
                await connection.Connect();

                // Testing sending a plain message.
                TestResponse response = await connection.SendEncryptedMessage<TestResponse>(request, TimeSpan.FromSeconds(5));
                response.Should().NotBeNull();
                response.ShouldBeEquivalentTo(expectedResponse);

                await Task.Delay(100); // Wait while internal sender processes the message.
                IMessage inMessageTask = await connection.OutMessagesHistory.FirstAsync().ToTask();
                mockTransport.Verify(transport => transport.Send(inMessageTask.MessageBytes), Times.Once);

                await connection.Disconnect();
            }
        }

        [Test]
        public void Should_throw_on_response_timeout()
        {
            IServiceLocator serviceLocator = new ServiceLocator();

            var mockTransport = new Mock<ITransport>();

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);
            serviceLocator.RegisterInstance(Mock.Of<TransportConfig>());
            serviceLocator.RegisterInstance(TLRig.Default);
            serviceLocator.RegisterInstance<IMessageIdGenerator>(new TestMessageIdsGenerator());
            serviceLocator.RegisterType<IHashServices, HashServices>();
            serviceLocator.RegisterType<IEncryptionServices, EncryptionServices>();
            serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>(RegistrationType.Transient);

            var testAction = new Func<Task>(async () =>
            {
                using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
                {
                    await connection.Connect();
                    await connection.SendPlainMessage<TestResponse>(new TestRequest(), TimeSpan.FromSeconds(1));
                }
            });
            testAction.ShouldThrow<TimeoutException>();
        }

        [Test]
        public async Task Should_timeout_on_connect()
        {
            IServiceLocator serviceLocator = new ServiceLocator();

            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(transport => transport.ConnectAsync(It.IsAny<CancellationToken>())).Returns(() => Task.Delay(1000));

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);
            serviceLocator.RegisterInstance(Mock.Of<TransportConfig>());
            serviceLocator.RegisterInstance(TLRig.Default);
            serviceLocator.RegisterInstance<IMessageIdGenerator>(new TestMessageIdsGenerator());
            serviceLocator.RegisterType<IHashServices, HashServices>();
            serviceLocator.RegisterType<IEncryptionServices, EncryptionServices>();
            serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>(RegistrationType.Transient);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                connection.DefaultConnectTimeout = TimeSpan.FromMilliseconds(100);
                MTProtoConnectResult result = await connection.Connect();
                result.ShouldBeEquivalentTo(MTProtoConnectResult.Timeout);
            }
        }
    }
}
