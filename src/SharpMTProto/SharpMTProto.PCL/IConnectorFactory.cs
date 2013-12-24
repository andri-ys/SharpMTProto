// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IConnectorFactory.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    /// <summary>
    ///     Interface for connector factory. Allows to create new connectors.
    /// </summary>
    public interface IConnectorFactory
    {
        /// <summary>
        ///     Creates a new connector.
        /// </summary>
        /// <returns>Connector.</returns>
        IConnector CreateConnector();
    }
}
