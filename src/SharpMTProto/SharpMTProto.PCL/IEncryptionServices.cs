// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IEncryptionServices.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    public interface IEncryptionServices
    {
        byte[] RSAEncrypt(byte[] data, byte[] publicKey);
    }
}
