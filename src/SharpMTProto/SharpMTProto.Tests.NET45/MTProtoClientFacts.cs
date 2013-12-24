// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
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
            LogManager.AddDebugListener(true);

            _serviceLocator = ServiceLocator.Default;
            _typeFactory = TypeFactory.Default;
        }

        private IServiceLocator _serviceLocator;
        private ITypeFactory _typeFactory;

        [Test]
        public async Task Should_create_auth_key()
        {
            var inConnector = new Subject<byte[]>();
            var mockConnector = new Mock<IConnector>();
            mockConnector.Setup(connector => connector.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));
            mockConnector.Setup(connector => connector.OnNext(TestData.ReqPQBytes)).Callback(() => inConnector.OnNext(TestData.ResPQBytes));

            _serviceLocator.RegisterInstance(Mock.Of<IConnectorFactory>(factory => factory.CreateConnector() == mockConnector.Object));
            _serviceLocator.RegisterInstance(TLRig.Default);
            _serviceLocator.RegisterInstance(Mock.Of<IMessageIdGenerator>(generator => generator.GetNextMessageId() == TestData.MessageId1));
            _serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>();
            
            var connection = _serviceLocator.ResolveType<IMTProtoConnection>();
            connection.DefaultRpcTimeout = TimeSpan.FromSeconds(5);

            var client = _typeFactory.CreateInstance<MTProtoClient>();
            
            var authKey = await client.CreateAuthKey(TestData.Nonce);
            authKey.ShouldAllBeEquivalentTo(TestData.AuthKey);
        }
    }
}
