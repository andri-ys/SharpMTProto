// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClient.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private const int HashLength = 20;
        private const int AuthRetryCount = 5;
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IEncryptionServices _encryptionServices;
        private readonly IHashServices _hashServices;
        private readonly IKeyChain _keyChain;
        private readonly INonceGenerator _nonceGenerator;
        private readonly TLRig _tlRig;
        private IMTProtoConnection _connection;
        private bool _isDisposed;

        public MTProtoClient([NotNull] IMTProtoConnection connection, [NotNull] TLRig tlRig, [NotNull] INonceGenerator nonceGenerator,
            [NotNull] IHashServices hashServices, [NotNull] IEncryptionServices encryptionServices, [NotNull] IKeyChain keyChain)
        {
            Argument.IsNotNull(() => connection);
            Argument.IsNotNull(() => tlRig);
            Argument.IsNotNull(() => nonceGenerator);
            Argument.IsNotNull(() => hashServices);
            Argument.IsNotNull(() => encryptionServices);
            Argument.IsNotNull(() => keyChain);

            _connection = connection;
            _tlRig = tlRig;
            _nonceGenerator = nonceGenerator;
            _hashServices = hashServices;
            _encryptionServices = encryptionServices;
            _keyChain = keyChain;
        }

        public async Task<byte[]> CreateAuthKey()
        {
            ThrowIfDisposed();

            byte[] authKey = null;
            Int128 nonce = _nonceGenerator.GetNonce(16).ToInt128();

            Log.Info(string.Format("Creating auth key with nonce 0x{0:X}.", nonce));
            try
            {
                await TryConnectIfDisconnected();


                // Requesting PQ.
                Log.Info("Requesting PQ (with nonce: 0x{0:X16})...", nonce);
                var resPQ = await _connection.ReqPqAsync(new ReqPqArgs {Nonce = nonce}) as ResPQ;
                if (resPQ == null)
                {
                    throw new InvalidResponseException();
                }
                CheckNonce(nonce, resPQ.Nonce);
                Log.Info(string.Format("Response PQ: [0x{0}], server nonce: [0x{1:X16}], {2}.", resPQ.Pq.ToHexaString(), resPQ.ServerNonce,
                    resPQ.ServerPublicKeyFingerprints.Aggregate("public keys fingerprints:", (text, fingerprint) => text + " [0x" + fingerprint.ToString("X8") + "]")));
                Int128 serverNonce = resPQ.ServerNonce;


                // Requesting DH params.
                PQInnerData pqInnerData;
                ReqDHParamsArgs reqDhParamsArgs = CreateReqDhParamsArgs(resPQ, out pqInnerData);
                Int256 newNonce = pqInnerData.NewNonce;

                Log.Info(string.Format("Requesting DH params with new nonce: [0x{0:X32}]...", newNonce));
                IServerDHParams serverDHParams = await _connection.ReqDHParamsAsync(reqDhParamsArgs);
                if (serverDHParams == null)
                {
                    throw new InvalidResponseException();
                }
                var dhParamsFail = serverDHParams as ServerDHParamsFail;
                if (dhParamsFail != null)
                {
                    if (CheckNewNonceHash(newNonce, dhParamsFail.NewNonceHash))
                    {
                        throw new MTProtoException("Requesting of the server DH params failed.");
                    }
                    throw new InvalidResponseException("The new nonce hash received from the server does NOT match with hash of the sent new nonce hash.");
                }
                var dhParamsOk = serverDHParams as ServerDHParamsOk;
                if (dhParamsOk == null)
                {
                    throw new InvalidResponseException();
                }
                CheckNonce(nonce, dhParamsOk.Nonce);
                CheckNonce(serverNonce, dhParamsOk.ServerNonce);
                Log.Info(string.Format("Received server DH params (encrypted answer: {0}).", dhParamsOk.EncryptedAnswer.ToHexaString(spaceEveryByte: true)));

                byte[] tmpAesKey;
                byte[] tmpAesIV;
                ComputeTmpAesKeyAndIV(newNonce, serverNonce, out tmpAesKey, out tmpAesIV);

                ServerDHInnerData serverDHInnerData = DecryptServerDHInnerData(dhParamsOk.EncryptedAnswer, tmpAesKey, tmpAesIV);
                // TODO: Implement checking.

                #region Checking instructions
                /****************************************************************************************************************************************
                 * Client is expected to check whether p = dh_prime is a safe 2048-bit prime (meaning that both p and (p-1)/2 are prime,
                 * and that 2^2047 < p < 2^2048), and that g generates a cyclic subgroup of prime order (p-1)/2, i.e. is a quadratic residue mod p.
                 * Since g is always equal to 2, 3, 4, 5, 6 or 7, this is easily done using quadratic reciprocity law,
                 * yielding a simple condition on p mod 4g — namely, p mod 8 = 7 for g = 2; p mod 3 = 2 for g = 3;
                 * no extra condition for g = 4; p mod 5 = 1 or 4 for g = 5; p mod 24 = 19 or 23 for g = 6; and p mod 7 = 3, 5 or 6 for g = 7.
                 * After g and p have been checked by the client, it makes sense to cache the result, so as not to repeat lengthy computations in future.
                 * 
                 * If the verification takes too long time (which is the case for older mobile devices), one might initially
                 * run only 15 Miller—Rabin iterations for verifying primeness of p and (p - 1)/2 with error probability not exceeding
                 * one billionth, and do more iterations later in the background.
                 * 
                 * Another optimization is to embed into the client application code a small table with some known “good” couples (g,p)
                 * (or just known safe primes p, since the condition on g is easily verified during execution),
                 * checked during code generation phase, so as to avoid doing such verification during runtime altogether.
                 * Server changes these values rarely, thus one usually has to put the current value of server's dh_prime into such a table.
                 * 
                 * For example, current value of dh_prime equals (in big-endian byte order):
                 * C71CAEB9C6B1C9048E6C522F70F13F73980D40238E3E21C14934D037563D930F48198A0AA7C14058229493D22530F4DBFA336F6E0AC925139543AED44CCE7C3720FD51
                 * F69458705AC68CD4FE6B6B13ABDC9746512969328454F18FAF8C595F642477FE96BB2A941D5BCD1D4AC8CC49880708FA9B378E3C4F3A9060BEE67CF9A4A4A695811051
                 * 907E162753B56B0F6B410DBA74D8A84B2A14B3144E0EF1284754FD17ED950D5965B4B9DD46582DB1178D169C6BC465B0D6FF9CA3928FEF5B9AE4E418FC15E83EBEA0F8
                 * 7FA9FF5EED70050DED2849F47BF959D956850CE929851F0D8115F635B105EE2E4E15D04B2454BF6F4FADF034B10403119CD8E3B92FCC5B
                 ****************************************************************************************************************************************/
                #endregion

                byte[] authKeyAuxHash = null;

                for (int retry = 0; retry < AuthRetryCount; retry++)
                {
                    byte[] b = _nonceGenerator.GetNonce(256);
                    byte[] g = serverDHInnerData.G.ToBytes(false);
                    byte[] ga = serverDHInnerData.GA;
                    byte[] p = serverDHInnerData.DhPrime;

                    DHOutParams dhOutParams = _encryptionServices.DH(b, g, ga, p);
                    authKey = dhOutParams.S;

                    var clientDHInnerData = new ClientDHInnerData
                    {
                        Nonce = nonce,
                        ServerNonce = serverNonce,
                        RetryId = authKeyAuxHash == null ? 0 : authKeyAuxHash.ToUInt64(),
                        GB = dhOutParams.GB
                    };

                    // byte[] authKeyHash = ComputeSHA1(authKey).Skip(HashLength - 8).Take(8).ToArray(); // Not used in client.
                    authKeyAuxHash = ComputeSHA1(authKey).Take(8).ToArray();

                    byte[] data = _tlRig.Serialize(clientDHInnerData);

                    // data_with_hash := SHA1(data) + data + (0-15 random bytes); such that length be divisible by 16;
                    byte[] dataWithHash = PrependHashAndAlign(data, 16);

                    // encrypted_data := AES256_ige_encrypt (data_with_hash, tmp_aes_key, tmp_aes_iv);
                    byte[] encryptedData = _encryptionServices.Aes256IgeEncrypt(dataWithHash, tmpAesKey, tmpAesIV);
                    var setClientDHParamsArgs = new SetClientDHParamsArgs {Nonce = nonce, ServerNonce = serverNonce, EncryptedData = encryptedData};

                    ISetClientDHParamsAnswer setClientDHParamsAnswer = await _connection.SetClientDHParamsAsync(setClientDHParamsArgs);
                    var dhGenOk = setClientDHParamsAnswer as DhGenOk;
                    if (dhGenOk != null)
                    {
                        CheckNonce(nonce, dhGenOk.Nonce);
                        CheckNonce(serverNonce, dhGenOk.ServerNonce);
                        Int128 newNonceHash1 = ComputeNewNonceHash(newNonce, 1, authKeyAuxHash);
                        if (dhGenOk.NewNonceHash1 != newNonceHash1)
                        {
                            throw new InvalidResponseException("New nonce hash 1 (DhGenOk) is invalid.");
                        }
                        return authKey;
                    }
                    var dhGenRetry = setClientDHParamsAnswer as DhGenRetry;
                    if (dhGenRetry != null)
                    {
                        CheckNonce(nonce, dhGenRetry.Nonce);
                        CheckNonce(serverNonce, dhGenRetry.ServerNonce);
                        Int128 newNonceHash2 = ComputeNewNonceHash(newNonce, 2, authKeyAuxHash);
                        if (dhGenRetry.NewNonceHash2 != newNonceHash2)
                        {
                            throw new InvalidResponseException("New nonce hash 2 (DhGenRetry) is invalid.");
                        }
                        continue;
                    }
                    var dhGenFail = setClientDHParamsAnswer as DhGenFail;
                    if (dhGenFail != null)
                    {
                        CheckNonce(nonce, dhGenFail.Nonce);
                        CheckNonce(serverNonce, dhGenFail.ServerNonce);
                        Int128 newNonceHash3 = ComputeNewNonceHash(newNonce, 3, authKeyAuxHash);
                        if (dhGenFail.NewNonceHash3 != newNonceHash3)
                        {
                            throw new InvalidResponseException("New nonce hash 3 (DhGenFail) is invalid.");
                        }
                        throw new MTProtoException("Failed to set client DH params.");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not create auth key.");
            }
            return authKey;
        }

        private Int128 ComputeNewNonceHash(Int256 newNonce, byte num, byte[] authKeyAuxHash)
        {
            var arr = new byte[33 + authKeyAuxHash.Length];
            using (var streamer = new TLStreamer(arr))
            {
                streamer.WriteInt256(newNonce);
                streamer.WriteByte(num);
                streamer.Write(authKeyAuxHash);
            }
            byte[] hash = ComputeSHA1(arr);
            Int128 result = hash.ToInt128(HashLength - 16);
            return result;
        }

        private byte[] PrependHashAndAlign(byte[] data, int alignment)
        {
            int dataLength = data.Length;
            byte[] dataHash = ComputeSHA1(data);
            int length = HashLength + dataLength;
            length += alignment - length%alignment;
            var dataWithHash = new byte[length];
            using (var streamer = new TLStreamer(dataWithHash))
            {
                streamer.Write(dataHash);
                streamer.Write(data);
                streamer.WriteRandomDataTillEnd();
            }
            return dataWithHash;
        }

        private void CheckNonce(Int128 expectedNonce, Int128 actualNonce)
        {
            if (actualNonce != expectedNonce)
            {
                throw new InvalidResponseException(string.Format("Expected nonce [0x{0:X16}] differs from the actual nonce [0x{1:X16}].", expectedNonce, actualNonce));
            }
        }

        private ServerDHInnerData DecryptServerDHInnerData(byte[] encryptedAnswer, byte[] tmpAesKey, byte[] tmpAesIV)
        {
            /* encrypted_answer := AES256_ige_encrypt (answer_with_hash, tmp_aes_key, tmp_aes_iv);
             * here, tmp_aes_key is a 256-bit key, and tmp_aes_iv is a 256-bit initialization vector.
             * The same as in all the other instances that use AES encryption,
             * the encrypted data is padded with random bytes to a length divisible by 16 immediately prior to encryption.
             */

            // Decrypting.
            byte[] answerWithHash = _encryptionServices.Aes256IgeDecrypt(encryptedAnswer, tmpAesKey, tmpAesIV);
            if ((answerWithHash.Length%16) != 0)
            {
                throw new InvalidResponseException("Decrypted ServerDHInnerData with hash has invalid length.");
            }

            var answerHash = new byte[HashLength];
            ServerDHInnerData serverDHInnerData;
            using (var streamer = new TLStreamer(answerWithHash))
            {
                streamer.Read(answerHash, 0, answerHash.Length);
                serverDHInnerData = _tlRig.Deserialize<ServerDHInnerData>(streamer);
            }

            // Checking the hash.
            byte[] serverDHInnerDataBytes = _tlRig.Serialize(serverDHInnerData);
            byte[] serverDHInnerDataBytesHash = ComputeSHA1(serverDHInnerDataBytes);
            if (!serverDHInnerDataBytesHash.SequenceEqual(answerHash))
            {
                throw new InvalidResponseException("Decrypted ServerDHInnerData hash is invalid.");
            }

            return serverDHInnerData;
        }

        private void ComputeTmpAesKeyAndIV(Int256 newNonce, Int128 serverNonce, out byte[] tmpAesKey, out byte[] tmpAesIV)
        {
            byte[] newNonceBytes = newNonce.ToBytes();
            byte[] serverNonceBytes = serverNonce.ToBytes();

            // SHA1(new_nonce + server_nonce).
            byte[] nPsHash = ComputeSHA1(ArrayUtils.Combine(newNonceBytes, serverNonceBytes));

            // SHA1(server_nonce + new_nonce).
            byte[] spnHash = ComputeSHA1(ArrayUtils.Combine(serverNonceBytes, newNonceBytes));

            // SHA1(new_nonce + new_nonce).
            byte[] npnHash = ComputeSHA1(ArrayUtils.Combine(newNonceBytes, newNonceBytes));

            // tmp_aes_key := SHA1(new_nonce + server_nonce) + substr (SHA1(server_nonce + new_nonce), 0, 12);
            tmpAesKey = new byte[32];
            Buffer.BlockCopy(nPsHash, 0, tmpAesKey, 0, HashLength);
            Buffer.BlockCopy(spnHash, 0, tmpAesKey, HashLength, 12);

            // tmp_aes_iv := substr (SHA1(server_nonce + new_nonce), 12, 8) + SHA1(new_nonce + new_nonce) + substr (new_nonce, 0, 4);
            tmpAesIV = new byte[32];
            Buffer.BlockCopy(spnHash, 12, tmpAesIV, 0, 8);
            Buffer.BlockCopy(npnHash, 0, tmpAesIV, 8, HashLength);
            Buffer.BlockCopy(newNonceBytes, 0, tmpAesIV, 28, 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte[] ComputeSHA1(byte[] data)
        {
            byte[] r = _hashServices.ComputeSHA1(data);
            Debug.Assert(r.Length == HashLength, "SHA1 must always be 20 bytes length.");
            return r;
        }

        private bool CheckNewNonceHash(Int256 newNonce, Int128 newNonceHash)
        {
            byte[] hash = ComputeSHA1(newNonce.ToBytes());
            Int128 nonceHash = hash.ToInt128();
            return nonceHash == newNonceHash;
        }

        private ReqDHParamsArgs CreateReqDhParamsArgs(ResPQ resPQ, out PQInnerData pqInnerData)
        {
            Int256 pq = resPQ.Pq.ToInt256(asLittleEndian: false);
            Int256 p, q;
            pq.GetPrimeMultipliers(out p, out q);

            Int256 newNonce = _nonceGenerator.GetNonce(32).ToInt256();
            pqInnerData = new PQInnerData
            {
                Pq = resPQ.Pq,
                P = p.ToBytes(false, true),
                Q = q.ToBytes(false, true),
                Nonce = resPQ.Nonce,
                ServerNonce = resPQ.ServerNonce,
                NewNonce = newNonce
            };

            byte[] data = _tlRig.Serialize(pqInnerData);
            byte[] dataHash = ComputeSHA1(data);

            Debug.Assert((dataHash.Length + data.Length) <= 255);

            // data_with_hash := SHA1(data) + data + (any random bytes); such that the length equal 255 bytes;
            var dataWithHash = new byte[255];
            using (var streamer = new TLStreamer(dataWithHash))
            {
                streamer.Write(dataHash);
                streamer.Write(data);
                streamer.WriteRandomDataTillEnd();
            }

            PublicKey publicKey = _keyChain.GetFirst(resPQ.ServerPublicKeyFingerprints);
            if (publicKey == null)
            {
                throw new PublicKeyNotFoundException(resPQ.ServerPublicKeyFingerprints);
            }

            byte[] encryptedData = _encryptionServices.RSAEncrypt(dataWithHash, publicKey);

            var reqDhParamsArgs = new ReqDHParamsArgs
            {
                Nonce = pqInnerData.Nonce,
                ServerNonce = pqInnerData.ServerNonce,
                P = pqInnerData.P,
                Q = pqInnerData.Q,
                PublicKeyFingerprint = publicKey.Fingerprint,
                EncryptedData = encryptedData
            };

            return reqDhParamsArgs;
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
