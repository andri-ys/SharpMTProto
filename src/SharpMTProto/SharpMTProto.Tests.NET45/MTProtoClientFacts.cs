// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Catel.IoC;
using Catel.Logging;
using FluentAssertions;
using Moq;
using MTProtoSchema;
using NUnit.Framework;
using SharpTL;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class MTProtoClientFacts
    {
        [SetUp]
        public void SetUp()
        {
            LogManager.AddDebugListener();

            _serviceLocator = ServiceLocator.Default;
            _typeFactory = TypeFactory.Default;
        }

        private IServiceLocator _serviceLocator;
        private ITypeFactory _typeFactory;

        [Test]
        public void Should_create_auth_key()
        {
            var mockConnection = new Mock<IMTProtoConnection>();
            mockConnection.Setup(connection => connection.SendUnencryptedMessage(It.Is<byte[]>(bytes => bytes.SequenceEqual(TestData.ReqPQBytes)))).Verifiable();
            mockConnection.Setup(connection => connection.ReceiveUnencryptedMessage()).Returns(() => TestData.ResPQBytes).Verifiable();

            var mockConnectionManager = new Mock<IMTProtoConnectionManager>();
            mockConnectionManager.Setup(manager => manager.CreateConnection()).Returns(() => mockConnection.Object).Verifiable();

            _serviceLocator.RegisterInstance(mockConnectionManager.Object);
            _serviceLocator.RegisterInstance(TLRig.Default);
            _serviceLocator.RegisterType<IMTProtoProxy, MTProtoProxy>(RegistrationType.Transient);

            var client = _typeFactory.CreateInstance<MTProtoClient>();

            client.CreateAuthKey(TestData.Nonce);

            mockConnection.Verify();
        }

        [Test]
        public void Should_req_pq()
        {
            var mockConnection = new Mock<IMTProtoConnection>();
            mockConnection.Setup(c => c.SendUnencryptedMessage(It.Is<byte[]>(bytes => bytes.SequenceEqual(TestData.ReqPQBytes)))).Verifiable();
            mockConnection.Setup(c => c.ReceiveUnencryptedMessage()).Returns(() => TestData.ResPQBytes).Verifiable();

            var mockConnectionManager = new Mock<IMTProtoConnectionManager>();
            mockConnectionManager.Setup(manager => manager.CreateConnection()).Returns(() => mockConnection.Object).Verifiable();

            ServiceLocator.Default.RegisterInstance(mockConnectionManager.Object);
            ServiceLocator.Default.RegisterInstance(TLRig.Default);
            ServiceLocator.Default.RegisterType<IMTProtoProxy, MTProtoProxy>(RegistrationType.Transient);

            var proxy = ServiceLocator.Default.ResolveType<IMTProtoProxy>();

            var resPq = proxy.req_pq(TestData.ReqPQ) as resPQ;

            resPq.Should().NotBeNull();
            resPq.ShouldBeEquivalentTo(TestData.ResPQ);

            mockConnection.Verify();
        }
    }
}
