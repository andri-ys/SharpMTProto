// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptionServices.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Security.Cryptography;
using BigMath;

namespace SharpMTProto.Services
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

        public byte[] Aes256IgeEncrypt(byte[] data, byte[] key, byte[] iv)
        {
            var iv1 = new byte[iv.Length/2];
            var iv2 = new byte[iv.Length/2];
            Array.Copy(iv, 0, iv1, 0, iv1.Length);
            Array.Copy(iv, iv.Length/2, iv2, 0, iv2.Length);

            using (var aes = new AesCryptoServiceProvider())
            {
                aes.Mode = CipherMode.ECB;
                aes.KeySize = key.Length*8;
                aes.Padding = PaddingMode.None;
                aes.IV = iv1;
                aes.Key = key;

                int blockSize = aes.BlockSize/8;

                var xPrev = new byte[blockSize];
                ;
                Buffer.BlockCopy(iv2, 0, xPrev, 0, blockSize);
                var yPrev = new byte[blockSize];
                Buffer.BlockCopy(iv1, 0, yPrev, 0, blockSize);

                using (var encrypted = new MemoryStream())
                {
                    using (var bw = new BinaryWriter(encrypted))
                    {
                        var x = new byte[blockSize];

                        ICryptoTransform encryptor = aes.CreateEncryptor();

                        for (int i = 0; i < data.Length; i += blockSize)
                        {
                            Buffer.BlockCopy(data, i, x, 0, blockSize);
                            byte[] y = Xor(encryptor.TransformFinalBlock(Xor(x, yPrev), 0, blockSize), xPrev);

                            Buffer.BlockCopy(x, 0, xPrev, 0, blockSize);
                            Buffer.BlockCopy(y, 0, yPrev, 0, blockSize);

                            bw.Write(y);
                        }
                    }
                    return encrypted.ToArray();
                }
            }
        }

        public byte[] Aes256IgeDecrypt(byte[] encryptedData, byte[] key, byte[] iv)
        {
            var iv1 = new byte[iv.Length/2];
            var iv2 = new byte[iv.Length/2];
            Array.Copy(iv, 0, iv1, 0, iv1.Length);
            Array.Copy(iv, iv.Length/2, iv2, 0, iv2.Length);

            using (var aes = new AesCryptoServiceProvider())
            {
                aes.Mode = CipherMode.ECB;
                aes.KeySize = key.Length*8;
                aes.Padding = PaddingMode.None;
                aes.IV = iv1;
                aes.Key = key;

                int blockSize = aes.BlockSize/8;

                var xPrev = new byte[blockSize];
                Buffer.BlockCopy(iv1, 0, xPrev, 0, blockSize);
                var yPrev = new byte[blockSize];
                Buffer.BlockCopy(iv2, 0, yPrev, 0, blockSize);

                using (var decrypted = new MemoryStream())
                {
                    using (var bw = new BinaryWriter(decrypted))
                    {
                        var x = new byte[blockSize];
                        ICryptoTransform decryptor = aes.CreateDecryptor();

                        for (int i = 0; i < encryptedData.Length; i += blockSize)
                        {
                            Buffer.BlockCopy(encryptedData, i, x, 0, blockSize);
                            byte[] y = Xor(decryptor.TransformFinalBlock(Xor(x, yPrev), 0, blockSize), xPrev);

                            Buffer.BlockCopy(x, 0, xPrev, 0, blockSize);
                            Buffer.BlockCopy(y, 0, yPrev, 0, blockSize);

                            bw.Write(y);
                        }
                    }
                    return decrypted.ToArray();
                }
            }
        }

        public DHOutParams DH(byte[] b, byte[] g, byte[] ga, byte[] p)
        {
            var bi = new BigInteger(b);
            var gi = new BigInteger(g);
            var gai = new BigInteger(ga);
            var pi = new BigInteger(p);

            byte[] gb = BigInteger.ModPow(gi, bi, pi).ToByteArray();
            byte[] s = BigInteger.ModPow(gai, bi, pi).ToByteArray();

            return new DHOutParams(gb, s);
        }

        private static byte[] Xor(byte[] buffer1, byte[] buffer2)
        {
            var result = new byte[buffer1.Length];
            for (int i = 0; i < buffer1.Length; i++)
            {
                result[i] = (byte) (buffer1[i] ^ buffer2[i]);
            }
            return result;
        }
    }
}
