// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IConnector.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.IO;

namespace SharpMTProto
{
    public enum ConnectorState
    {
        Disconnected = 0,
        Connected = 1
    }

    public interface IConnector
    {
        bool IsConnected { get; }

        Stream InStream { get; }

        Stream OutStream { get; }

        ConnectorState State { get; }
        void Connect();
    }
}
