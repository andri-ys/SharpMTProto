// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptionServices.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using BigMath;

namespace SharpMTProto.Extra
{
    public class EncryptionServices : IEncryptionServices
    {
        public byte[] RSAEncrypt(byte[] data, PublicKey publicKey)
        {
            var m = new BigInteger(publicKey.Modulus);
            var e = new BigInteger(publicKey.Exponent);
            var r = new BigInteger(data);
            BigInteger s = BigInteger.ModPow(r, e, m);
            byte[] temp = s.ToByteArray();
            return temp;
        }
    }
}
