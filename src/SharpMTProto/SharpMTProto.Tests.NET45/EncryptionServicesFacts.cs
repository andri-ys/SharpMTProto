// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptionServicesFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using BigMath.Utils;
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

        [Test]
        public void Should_encrypt_RSA()
        {
            var es = new EncryptionServices();
            var encryptedData = es.RSAEncrypt(DataWithHash, RSAKey);
            encryptedData.ShouldBeEquivalentTo(EncryptedData);
        }

        public static readonly byte[] DataWithHash =
            "DB761C27718A2305044F71F2AD951629D78B2449EC5AC9830817ED48941A08F98100000004494C553B00000004539110730000003E0549828CCA27E966B301A48FECE2FCA5CF4D33F4A11EA877BA4AA573907330311C85DB234AA2640AFC4A76A735CF5B1F0FD68BD17FA181E1229AD867CC024DB546D0B124AE942AE024728A5A903D9908653B465B40742DF237ED10A993F3830028488EC6A4D9CC9EE382BA30297DFB4FB22793C1E65C75D1A3BFECC083804C30EF8A3248AED3648B6EA877BDBAFCC9B9F2C1F76B5FC119CBD2BC23E4EC0C1A623689419E4AB2A0DFC308533DE4B817A6F632CEC189E1D02D716676F2AF7F9F628D148630BB2A6F3F2018"
                .HexToBytes();

        public static readonly byte[] EncryptedData =
            "310CEFEAFE3234A275164BCBD4FCE205449E4411582C2E522EB263A05B54426E500999C779BDB68AFE4B56C6E34ED4E6786426BFB02A284AEA80F763EDDFC9FAE338EA34608D389EDAA6663FBF69DFE04A88B2EF22E9E9468B6D40D1796B978752D0AA34B8F2BB729E13B8A093CD360A44AB82E08F13CC6AC9180BCB6BE3F179C24D19D4B0202DD880B4EA09266DCC49B266BF51404DE71EFF86FB544353B5541BFACA820C2DFEC30FDF9E65AB93390ABDE21DD8F2DE7A5D0244E62787A1FE13C6DC588FCC22ECBB3781DCAD09B9372E128510CFEFD338B1234C16A74F83B46F768C70E71C662B752446F81C199316F1EA98B9CF8110A3081F1840756265FA3C"
                .HexToBytes();

        private static readonly PublicKey RSAKey =
            new PublicKey(
                "0c150023e2f70db7985ded064759cfecf0af328e69a41daf4d6f01b538135a6f91f8f8b2a0ec9ba9720ce352efcf6c5680ffc424bd634864902de0b4bd6d49f4e580230e3ae97d95c8b19442b3c0a10d8f5633fecedd6926a7f6dab0ddb7d457f9ea81b8465fcd6fffeed114011df91c059caedaf97625f6c96ecc74725556934ef781d866b34f011fce4d835a090196e9a5f0e4449af7eb697ddb9076494ca5f81104a305b6dd27665722c46b60e5df680fb16b210607ef217652e60236c255f6a28315f4083a96791d7214bf64c1df4fd0db1944fb26a2a57031b32eee64ad15a8ba68885cde74a5bfc920f6abf59ba5c75506373e7130f9042da922179251f",
                "010001", 0xc3b42b026ce86b21UL);
    }
}
