// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IMTProtoConnection.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace SharpMTProto
{
    public interface IMTProtoConnection : IDisposable
    {
        UnencryptedMessage SendUnencryptedMessage(byte[] messageData);
        byte[] ReceiveUnencryptedMessage();
        void Close();
    }
}
