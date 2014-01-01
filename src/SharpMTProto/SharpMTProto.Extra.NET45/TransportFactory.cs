// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TransportFactory.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using SharpMTProto.Transport;

namespace SharpMTProto.Extra
{
    public class TransportFactory : ITransportFactory
    {
        public ITransport CreateTransport(TransportConfig transportConfig)
        {
            // TCP.
            var tcpTransportConfig = transportConfig as TcpTransportConfig;
            if (tcpTransportConfig != null)
            {
                return new TcpTransport(tcpTransportConfig);
            }

            throw new NotSupportedException(string.Format("Transport type '{0}' is not supported.", transportConfig.TransportName));
        }
    }
}
