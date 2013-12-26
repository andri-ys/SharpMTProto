// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptionServices.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Security.Cryptography;

namespace SharpMTProto.Extra
{
    public class EncryptionServices : IEncryptionServices
    {
        public byte[] RSAEncrypt(byte[] data, byte[] publicKey)
        {
            using(var rsa = new RSACryptoServiceProvider())
            {
                rsa.ImportCspBlob(publicKey);
                return rsa.Encrypt(data, true);
            }
        }
    }
}