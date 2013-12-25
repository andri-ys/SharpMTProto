// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NonceGenerator.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace SharpMTProto
{
    public interface INonceGenerator
    {
        byte[] GetNonce(uint length);
    }

    public class NonceGenerator : INonceGenerator
    {
        private readonly Random _random;

        public NonceGenerator()
        {
            _random = new Random();
        }

        public byte[] GetNonce(uint length)
        {
            var nonce = new byte[length];
            _random.NextBytes(nonce);
            return nonce;
        }
    }
}
