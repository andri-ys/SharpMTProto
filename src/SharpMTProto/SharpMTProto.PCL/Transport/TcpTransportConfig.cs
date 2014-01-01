// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpTransportConfig.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace SharpMTProto.Transport
{
    public class TcpTransportConfig : TransportConfig
    {
        public TcpTransportConfig(string ipAddress, int port)
        {
            IPAddress = ipAddress;
            Port = port;
            ConnectTimeout = TimeSpan.FromMilliseconds(5000);
            MaxBufferSize = 2048;
        }

        public string IPAddress { get; set; }

        public int Port { get; set; }
        public int MaxBufferSize { get; set; }

        public TimeSpan ConnectTimeout { get; set; }


        public override string TransportName
        {
            get { return "TCP"; }
        }
    }
}
