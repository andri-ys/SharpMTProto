// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClient.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Catel;
using Catel.Logging;
using SharpMTProto.Annotations;
using SharpMTProto.Transport;
using SharpTL;

namespace SharpMTProto
{
    /// <summary>
    ///     MTProto client.
    /// </summary>
    [UsedImplicitly]
    public partial class MTProtoClient : IDisposable
    {
        private const int HashLength = 20;
        private const int AuthRetryCount = 5;
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IMTProtoConnectionFactory _connectionFactory;
        private readonly IEncryptionServices _encryptionServices;
        private readonly IHashServices _hashServices;
        private readonly IKeyChain _keyChain;
        private readonly ITransportConfigProvider _transportConfigProvider;
        private readonly INonceGenerator _nonceGenerator;
        private readonly TLRig _tlRig;
        private bool _isDisposed;

        public MTProtoClient([NotNull] IMTProtoConnectionFactory connectionFactory, [NotNull] TLRig tlRig, [NotNull] INonceGenerator nonceGenerator,
            [NotNull] IHashServices hashServices, [NotNull] IEncryptionServices encryptionServices, [NotNull] IKeyChain keyChain,
            [NotNull] ITransportConfigProvider transportConfigProvider)
        {
            Argument.IsNotNull(() => connectionFactory);
            Argument.IsNotNull(() => tlRig);
            Argument.IsNotNull(() => nonceGenerator);
            Argument.IsNotNull(() => hashServices);
            Argument.IsNotNull(() => encryptionServices);
            Argument.IsNotNull(() => keyChain);
            Argument.IsNotNull(() => transportConfigProvider);

            _connectionFactory = connectionFactory;
            _tlRig = tlRig;
            _nonceGenerator = nonceGenerator;
            _hashServices = hashServices;
            _encryptionServices = encryptionServices;
            _keyChain = keyChain;
            _transportConfigProvider = transportConfigProvider;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte[] ComputeSHA1(byte[] data)
        {
            byte[] r = _hashServices.ComputeSHA1(data);
            Debug.Assert(r.Length == HashLength, "SHA1 must always be 20 bytes length.");
            return r;
        }
    }
}
