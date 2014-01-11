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
        public async Task Should_send_and_receive_unencrypted_message()
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
            serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>(RegistrationType.Transient);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                await connection.Connect();

                // Testing sending.
                var message = new UnencryptedMessage(0x0102030405060708UL, messageData);
                connection.SendUnencryptedMessage(message);

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
        public async Task Should_send_and_wait_for_response()
        {
            IServiceLocator serviceLocator = new ServiceLocator();

            var request = new TestRequest {TestId = 9};
            var expectedResponse = new TestResponse {TestId = 9, TestText = "Number 1"};
            var expectedResponseMessage = new UnencryptedMessage(0x0102030405060708, TLRig.Default.Serialize(expectedResponse));

            var inConnector = new Subject<byte[]>();

            var mockTransport = new Mock<ITransport>();
            mockTransport.Setup(connector => connector.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));
            mockTransport.Setup(connector => connector.Send(It.IsAny<byte[]>())).Callback(() => inConnector.OnNext(expectedResponseMessage.MessageBytes));

            var mockTransportFactory = new Mock<ITransportFactory>();
            mockTransportFactory.Setup(manager => manager.CreateTransport(It.IsAny<TransportConfig>())).Returns(() => mockTransport.Object).Verifiable();

            serviceLocator.RegisterInstance(mockTransportFactory.Object);
            serviceLocator.RegisterInstance(Mock.Of<TransportConfig>());
            serviceLocator.RegisterInstance(TLRig.Default);
            serviceLocator.RegisterInstance<IMessageIdGenerator>(new TestMessageIdsGenerator());
            serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>(RegistrationType.Transient);

            using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
            {
                await connection.Connect();

                // Testing sending.
                TestResponse response = await connection.SendUnencryptedMessageAndWaitForResponse<TestResponse>(request, TimeSpan.FromSeconds(5));
                response.Should().NotBeNull();
                response.ShouldBeEquivalentTo(expectedResponse);

                await Task.Delay(100); // Wait while internal sender processes the message.
                IMessage inMessageTask = await connection.OutMessagesHistory.FirstAsync().ToTask();
                mockTransport.Verify(connector => connector.Send(inMessageTask.MessageBytes), Times.Once);

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
            serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>(RegistrationType.Transient);

            var testAction = new Func<Task>(async () =>
            {
                using (var connection = serviceLocator.ResolveType<IMTProtoConnection>())
                {
                    await connection.Connect();
                    await connection.SendUnencryptedMessageAndWaitForResponse<TestResponse>(new TestRequest(), TimeSpan.FromSeconds(1));
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
