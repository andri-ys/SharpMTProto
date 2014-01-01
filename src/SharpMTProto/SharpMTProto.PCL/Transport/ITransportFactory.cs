// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TransportFactory.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Transport
{
    /// <summary>
    ///     Interface for a transport factory. Allows to create new transports.
    /// </summary>
    public interface ITransportFactory
    {
        /// <summary>
        ///     Creates a new TCP transport.
        /// </summary>
        /// <param name="transportConfig">Transport info.</param>
        /// <returns>TCP transport.</returns>
        ITransport CreateTransport(TransportConfig transportConfig);
    }
}
