// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthInfo.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace SharpMTProto
{
    /// <summary>
    ///     Auth info contains of auth key and initial salt.
    /// </summary>
    public struct AuthInfo
    {
        private readonly byte[] _authKey;
        private readonly UInt64 _initialSalt;

        public AuthInfo(byte[] authKey, ulong initialSalt)
        {
            _authKey = authKey;
            _initialSalt = initialSalt;
        }

        public byte[] AuthKey
        {
            get { return _authKey; }
        }

        public ulong InitialSalt
        {
            get { return _initialSalt; }
        }
    }
}
