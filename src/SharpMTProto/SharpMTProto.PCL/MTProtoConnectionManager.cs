// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnectionManager.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    /// <summary>
    ///     MTProto connection manager. Allows to create new connections.
    /// </summary>
    public class MTProtoConnectionManager : IMTProtoConnectionManager
    {
        public IMTProtoConnection CreateConnection()
        {
            return new MTProtoConnection(null);
        }
    }
}
