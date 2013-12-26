// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClient.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BigMath;
using BigMath.Utils;
using Catel;
using Catel.Logging;
using MTProtoSchema;
using SharpMTProto.Annotations;
using SharpTL;

namespace SharpMTProto
{
    /// <summary>
    ///     MTProto client.
    /// </summary>
    [UsedImplicitly]
    public class MTProtoClient : IDisposable
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IEncryptionServices _encryptionServices;
        private readonly IHashServices _hashServices;
        private readonly INonceGenerator _nonceGenerator;
        private readonly TLRig _tlRig;
        private IMTProtoConnection _connection;
        private bool _isDisposed;

        public MTProtoClient([NotNull] IMTProtoConnection connection, [NotNull] TLRig tlRig, [NotNull] INonceGenerator nonceGenerator,
            [NotNull] IHashServices hashServices, [NotNull] IEncryptionServices encryptionServices)
        {
            Argument.IsNotNull(() => connection);
            Argument.IsNotNull(() => tlRig);
            Argument.IsNotNull(() => nonceGenerator);
            Argument.IsNotNull(() => hashServices);
            Argument.IsNotNull(() => encryptionServices);

            _connection = connection;
            _tlRig = tlRig;
            _nonceGenerator = nonceGenerator;
            _hashServices = hashServices;
            _encryptionServices = encryptionServices;
        }

        public async Task<byte[]> CreateAuthKey()
        {
            ThrowIfDisposed();

            Int128 nonce = _nonceGenerator.GetNonce(16).ToInt128();

            Log.Info(string.Format("Creating auth key with nonce 0x{0:X}.", nonce));
            try
            {
                await TryConnectIfDisconnected();

                var resPQ = await _connection.ReqPqAsync(new ReqPqArgs {Nonce = nonce}) as ResPQ;
                if (resPQ == null)
                {
                    throw new WrongResponseException();
                }
                if (resPQ.Nonce != nonce)
                {
                    throw new WrongResponseException(string.Format("Nonce in response ({0}) differs from the nonce in request ({1}).", resPQ.Nonce, nonce));
                }

                Int256 pq = resPQ.Pq.ToInt256(asLittleEndian: false);
                Int256 p, q;
                pq.GetPrimeMultipliers(out p, out q);

                Int256 newNonce = _nonceGenerator.GetNonce(32).ToInt256();
                var pqInnerData = new PQInnerData
                {
                    Pq = resPQ.Pq,
                    P = p.ToBytes(false, true),
                    Q = q.ToBytes(false, true),
                    Nonce = nonce,
                    ServerNonce = resPQ.ServerNonce,
                    NewNonce = newNonce
                };

                byte[] data = _tlRig.Serialize(pqInnerData);
                byte[] dataHash = _hashServices.ComputeSHA1(data);

                Debug.Assert((dataHash.Length + data.Length) <= 255);

                // data_with_hash := SHA1(data) + data + (any random bytes); such that the length equal 255 bytes;
                var dataWithHash = new byte[255];
                using (var streamer = new TLStreamer(dataWithHash))
                {
                    streamer.WriteAllBytes(dataHash);
                    streamer.WriteAllBytes(data);
                    streamer.WriteRandomDataTillEnd();
                }

//                _encryptionServices.RSAEncrypt(dataWithHash, )
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not create auth key.");
            }
            return null;
        }

        private async Task TryConnectIfDisconnected()
        {
            if (!_connection.IsConnected)
            {
                MTProtoConnectResult result = await _connection.Connect();
                if (result != MTProtoConnectResult.Success)
                {
                    throw new CouldNotConnectException("Connection trial was unsuccessful.");
                }
            }
        }

        #region Disposable
        public void Dispose()
        {
            Dispose(true);
        }

        protected void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName, "Can not access disposed client.");
            }
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;

            if (isDisposing)
            {
                if (_connection != null)
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }
        }
        #endregion
    }
}
