// --------------------------------------------------------------------------------------------------------------------
// <copyright file="KeyChainFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using FluentAssertions;
using NUnit.Framework;
using SharpMTProto.Extra;
using SharpTL;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class KeyChainFacts
    {
        [Test]
        public void Should_add_keys()
        {
            var keyChain = new KeyChain(TLRig.Default, new HashServices());

            keyChain.AddKeys(TestData.TestPublicKeys);

            foreach (PublicKey testKey in TestData.TestPublicKeys)
            {
                keyChain.Contains(testKey.Fingerprint).Should().BeTrue("Because key chain must have added key with fingerprint: {0:X16}.", testKey.Fingerprint);
                var key = keyChain[testKey.Fingerprint];
                key.Should().NotBeNull();
                key.ShouldBeEquivalentTo(testKey);
            }
        }

        [Test]
        public void Should_compute_key_fingerprint()
        {
            var keyChain = new KeyChain(TLRig.Default, new HashServices());

            foreach (PublicKey testKey in TestData.TestPublicKeys)
            {
                var actual = keyChain.ComputeFingerprint(testKey.Modulus, testKey.Exponent);
                actual.Should().Be(testKey.Fingerprint);
            }
        }

        [Test]
        public void Should_check_key_fingerprint()
        {
            var keyChain = new KeyChain(TLRig.Default, new HashServices());

            foreach (PublicKey testKey in TestData.TestPublicKeys)
            {
                keyChain.CheckKeyFingerprint(testKey).Should().BeTrue();
            }
        }
    }
}
