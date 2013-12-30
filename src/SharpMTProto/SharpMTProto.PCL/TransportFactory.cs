// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TransportFactory.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using Catel;
using Catel.IoC;

namespace SharpMTProto
{
    /// <summary>
    ///     Interface for a transport factory. Allows to create new transports.
    /// </summary>
    public interface ITransportFactory
    {
        /// <summary>
        ///     Creates a new transport.
        /// </summary>
        /// <returns>Transport.</returns>
        ITransport CreateTransport();
    }

    public class TransportFactory : ITransportFactory
    {
        private readonly IServiceLocator _serviceLocator;

        public TransportFactory(IServiceLocator serviceLocator)
        {
            Argument.IsNotNull(() => serviceLocator);

            _serviceLocator = serviceLocator;
        }

        public ITransport CreateTransport()
        {
            var transport = _serviceLocator.ResolveType<ITransport>();
            return transport;
        }
    }
}
