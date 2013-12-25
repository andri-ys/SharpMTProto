// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Catel.IoC;
using Catel.Logging;
using FluentAssertions;
using Moq;
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
        }

        [Test]
        public async Task Should_create_auth_key()
        {
            var serviceLocator = new ServiceLocator();
            var typeFactory = new TypeFactory(serviceLocator.ResolveType<IDependencyResolver>());

            var inConnector = new Subject<byte[]>();
            var mockConnector = new Mock<IConnector>();
            mockConnector.Setup(connector => connector.Subscribe(It.IsAny<IObserver<byte[]>>())).Callback<IObserver<byte[]>>(observer => inConnector.Subscribe(observer));
            mockConnector.Setup(connector => connector.OnNext(TestData.ReqPQBytes)).Callback(() => inConnector.OnNext(TestData.ResPQBytes));

            serviceLocator.RegisterInstance(Mock.Of<IConnectorFactory>(factory => factory.CreateConnector() == mockConnector.Object));
            serviceLocator.RegisterInstance(TLRig.Default);
            serviceLocator.RegisterInstance(TestData.MessageIdGenerator);
            serviceLocator.RegisterInstance(TestData.NonceGenerator);
            serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>();

            var connection = serviceLocator.ResolveType<IMTProtoConnection>();
            connection.DefaultRpcTimeout = TimeSpan.FromSeconds(5);

            var client = typeFactory.CreateInstance<MTProtoClient>();

            byte[] authKey = await client.CreateAuthKey();
            authKey.ShouldAllBeEquivalentTo(TestData.AuthKey);
        }
    }
}
