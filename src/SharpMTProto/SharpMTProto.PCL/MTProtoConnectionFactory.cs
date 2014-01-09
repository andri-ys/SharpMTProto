// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnectionFactory.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using Catel;
using Catel.IoC;
using SharpMTProto.Transport;

namespace SharpMTProto
{
    public interface IMTProtoConnectionFactory
    {
        TimeSpan DefaultRpcTimeout { get; set; }
        TimeSpan DefaultConnectTimeout { get; set; }
        IMTProtoConnection Create(TransportConfig transportConfig);
    }

    public class MTProtoConnectionFactory : IMTProtoConnectionFactory
    {
        private readonly IServiceLocator _serviceLocator;
        private readonly ITypeFactory _typeFactory;

        public MTProtoConnectionFactory(IServiceLocator serviceLocator)
        {
            Argument.IsNotNull(() => serviceLocator);

            _serviceLocator = serviceLocator;
            _typeFactory = _serviceLocator.ResolveType<ITypeFactory>();
        }

        public IMTProtoConnection Create(TransportConfig transportConfig)
        {
            var connection = _typeFactory.CreateInstanceWithParametersAndAutoCompletion<MTProtoConnection>(transportConfig);

            connection.DefaultRpcTimeout = DefaultRpcTimeout;
            connection.DefaultConnectTimeout = DefaultConnectTimeout;

            return connection;
        }

        public TimeSpan DefaultRpcTimeout { get; set; }
        public TimeSpan DefaultConnectTimeout { get; set; }
    }
}
