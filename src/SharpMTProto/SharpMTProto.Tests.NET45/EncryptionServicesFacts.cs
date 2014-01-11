// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptionServicesFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using FluentAssertions;
using NUnit.Framework;
using SharpMTProto.Services;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class EncryptionServicesFacts
    {
        [Test]
        public void Should_decrypt_Aes256Ige()
        {
            var es = new EncryptionServices();
            byte[] decryptedData = es.Aes256IgeDecrypt(TestData.ServerDHParamsOkEncryptedAnswer, TestData.TmpAesKey, TestData.TmpAesIV);
            decryptedData.ShouldAllBeEquivalentTo(TestData.ServerDHInnerDataWithHash);
        }

        [Test]
        public void Should_encrypt_Aes256Ige()
        {
            var es = new EncryptionServices();
            byte[] encryptedData = es.Aes256IgeEncrypt(TestData.ServerDHInnerDataWithHash, TestData.TmpAesKey, TestData.TmpAesIV);
            encryptedData.ShouldAllBeEquivalentTo(TestData.ServerDHParamsOkEncryptedAnswer);
        }
    }
}
