// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IConnector.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    public interface IConnector
    {
        void SendData(byte[] data);

        int ReceiveData(byte[] buffer, int offset, int count);
    }
}
