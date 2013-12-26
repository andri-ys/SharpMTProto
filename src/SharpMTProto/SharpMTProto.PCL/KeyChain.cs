// --------------------------------------------------------------------------------------------------------------------
// <copyright file="KeyChain.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BigMath.Utils;
using Catel;
using SharpMTProto.Annotations;
using SharpTL;

namespace SharpMTProto
{
    /// <summary>
    ///     Key chain.
    /// </summary>
    public class KeyChain : IEnumerable<Key>
    {
        private readonly IHashServices _hashServices;
        private readonly Dictionary<ulong, Key> _keys = new Dictionary<ulong, Key>();
        private readonly TLRig _tlRig;

        public KeyChain([NotNull] TLRig tlRig, [NotNull] IHashServices hashServices)
        {
            Argument.IsNotNull(() => tlRig);
            Argument.IsNotNull(() => hashServices);

            _tlRig = tlRig;
            _hashServices = hashServices;
        }

        public Key this[ulong keyFingerprint]
        {
            get { return _keys.ContainsKey(keyFingerprint) ? _keys[keyFingerprint] : null; }
        }

        public IEnumerator<Key> GetEnumerator()
        {
            return _keys.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(Key key)
        {
            if (!_keys.ContainsKey(key.Fingerprint))
            {
                _keys.Add(key.Fingerprint, key);
            }
        }

        public void AddKeys(params Key[] keys)
        {
            AddKeys(keys.AsEnumerable());
        }

        public void AddKeys(IEnumerable<Key> keys)
        {
            foreach (Key key in keys)
            {
                Add(key);
            }
        }

        public void Remove(ulong keyFingerprint)
        {
            if (!_keys.ContainsKey(keyFingerprint))
            {
                _keys.Remove(keyFingerprint);
            }
        }

        public bool Contains(ulong keyFingerprint)
        {
            return _keys.ContainsKey(keyFingerprint);
        }

        /// <summary>
        ///     Checks key fingerprint.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>True - fingerprint is OK, False - fingerprint is incorrect.</returns>
        public bool CheckKeyFingerprint(Key key)
        {
            byte[] keyData = _tlRig.Serialize(key, TLSerializationMode.Bare);
            byte[] hash = _hashServices.ComputeSHA1(keyData);
            ulong expectedFingerprint = hash.ToUInt64(hash.Length - 8, false);
            return key.Fingerprint == expectedFingerprint;
        }

        /// <summary>
        ///     Calculates fingerprint for a public RSA key.
        /// </summary>
        /// <param name="publicKey">Public key bytes.</param>
        /// <param name="exponent">Exponent bytes.</param>
        /// <returns>Returns fingerprint as lower 64 bits of the SHA1(RSAPublicKey).</returns>
        public ulong CalculateFingerprint(byte[] publicKey, byte[] exponent)
        {
            var tempKey = new Key(publicKey, exponent, 0);
            byte[] keyData = _tlRig.Serialize(tempKey, TLSerializationMode.Bare);
            return CalculateFingerprint(keyData);
        }

        /// <summary>
        ///     Calculates fingerprint for a public RSA key.
        /// </summary>
        /// <param name="keyData">Bare serialized type of a constructor: "rsa_public_key n:string e:string = RSAPublicKey".</param>
        /// <returns>Returns fingerprint as lower 64 bits of the SHA1(RSAPublicKey).</returns>
        public ulong CalculateFingerprint(byte[] keyData)
        {
            byte[] hash = _hashServices.ComputeSHA1(keyData);
            return hash.ToUInt64(asLittleEndian: false);
        }
    }
}
