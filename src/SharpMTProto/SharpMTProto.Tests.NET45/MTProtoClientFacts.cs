﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using BigMath.Utils;
using Catel.IoC;
using Catel.Logging;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SharpMTProto.Extra;
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
            mockConnector.Setup(connector => connector.OnNext(TestData.ReqPQ)).Callback(() => inConnector.OnNext(TestData.ResPQ));
            mockConnector.Setup(connector => connector.OnNext(TestData.ReqDHParams)).Callback(() => inConnector.OnNext(TestData.ServerDHParams));
            mockConnector.Setup(connector => connector.OnNext(TestData.SetClientDHParams)).Callback(() => inConnector.OnNext(TestData.DhGenOk));

            var mockEncryptionServices = new Mock<IEncryptionServices>();
            mockEncryptionServices.Setup(services => services.RSAEncrypt(It.IsAny<byte[]>(), It.IsAny<PublicKey>())).Returns(TestData.EncryptedData);
            mockEncryptionServices.Setup(services => services.Aes256IgeDecrypt(TestData.ServerDHParamsOkEncryptedAnswer, TestData.TmpAesKey, TestData.TmpAesIV))
                .Returns(TestData.ServerDHInnerDataWithHash);
            mockEncryptionServices.Setup(
                services =>
                    services.Aes256IgeEncrypt(It.Is<byte[]>(bytes => bytes.RewriteWithValue(0, bytes.Length - 12, 12).SequenceEqual(TestData.ClientDHInnerDataWithHash)),
                        TestData.TmpAesKey, TestData.TmpAesIV)).Returns(TestData.SetClientDHParamsEncryptedData);
            mockEncryptionServices.Setup(services => services.DH(TestData.B, TestData.G, TestData.GA, TestData.P)).Returns(new DHOutParams(TestData.GB, TestData.AuthKey));

            serviceLocator.RegisterInstance(Mock.Of<IConnectorFactory>(factory => factory.CreateConnector() == mockConnector.Object));
            serviceLocator.RegisterInstance(TLRig.Default);
            serviceLocator.RegisterInstance<IMessageIdGenerator>(new TestMessageIdsGenerator());
            serviceLocator.RegisterInstance<INonceGenerator>(new TestNonceGenerator());
            serviceLocator.RegisterType<IHashServices, HashServices>();
            serviceLocator.RegisterInstance(mockEncryptionServices.Object);
            serviceLocator.RegisterType<IKeyChain, KeyChain>();
            serviceLocator.RegisterType<IMTProtoConnection, MTProtoConnection>(RegistrationType.Transient);

            var keyChain = serviceLocator.ResolveType<IKeyChain>();
            keyChain.AddKeys(TestData.TestPublicKeys);

            var connection = serviceLocator.ResolveType<IMTProtoConnection>();
            connection.DefaultRpcTimeout = TimeSpan.FromSeconds(5);

            var client = typeFactory.CreateInstance<MTProtoClient>();

            byte[] authKey = await client.CreateAuthKey();
            authKey.ShouldAllBeEquivalentTo(TestData.AuthKey);
        }
    }
}
