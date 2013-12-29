// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnectionFactory.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using Catel;
using Catel.IoC;

namespace SharpMTProto
{
    public interface IMTProtoConnectionFactory
    {
        TimeSpan DefaultRpcTimeout { get; set; }
        TimeSpan DefaultConnectTimeout { get; set; }
        IMTProtoConnection Create();
    }

    public class MTProtoConnectionFactory : IMTProtoConnectionFactory
    {
        private readonly IServiceLocator _serviceLocator;

        public MTProtoConnectionFactory(IServiceLocator serviceLocator)
        {
            Argument.IsNotNull(() => serviceLocator);

            _serviceLocator = serviceLocator;
        }

        public IMTProtoConnection Create()
        {
            var connection = _serviceLocator.ResolveType<IMTProtoConnection>();

            connection.DefaultRpcTimeout = DefaultRpcTimeout;
            connection.DefaultConnectTimeout = DefaultConnectTimeout;

            return connection;
        }

        public TimeSpan DefaultRpcTimeout { get; set; }
        public TimeSpan DefaultConnectTimeout { get; set; }
    }
}
