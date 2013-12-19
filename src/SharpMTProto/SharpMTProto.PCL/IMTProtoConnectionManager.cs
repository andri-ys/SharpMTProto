// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IMTProtoConnectionManager.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    /// <summary>
    ///     Interface for MTProto connection manager. Allows to create new connections.
    /// </summary>
    public interface IMTProtoConnectionManager
    {
        /// <summary>
        ///     Creates a new connection.
        /// </summary>
        /// <returns>MTProto connection.</returns>
        IMTProtoConnection CreateConnection();
    }
}
