﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptedMessage.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using BigMath;
using BigMath.Utils;
using Catel;
using SharpMTProto.Annotations;
using SharpMTProto.Services;
using SharpTL;

namespace SharpMTProto
{
    /// <summary>
    ///     Type of a message sender.
    /// </summary>
    public enum Sender
    {
        Client,
        Server
    }

    /// <summary>
    ///     Encrypted message.
    ///     https://core.telegram.org/mtproto/description#encrypted-message
    /// </summary>
    public class EncryptedMessage : IMessage
    {
        /// <summary>
        ///     Outer header length in bytes (8 + 16).
        /// </summary>
        private const int OuterHeaderLength = 24;

        /// <summary>
        ///     Inner header length in bytes (8 + 8 + 8 + 4 + 4).
        /// </summary>
        private const int InnerHeaderLength = 32;

        private const int MsgKeyLength = 16;
        private const int Alignment = 16;

        [ThreadStatic] private static byte[] _aesKeyAndIVComputationBuffer;

        private readonly ulong _authKeyId;

        private readonly int _length;
        private readonly byte[] _messageBytes;
        private readonly byte[] _messageData;
        private readonly int _messageDataLength;
        private readonly ulong _messageId;
        private readonly Int128 _msgKey;
        private readonly ulong _salt;
        private readonly uint _seqNumber;
        private readonly ulong _sessionId;

        /// <summary>
        ///     Initializes a new instance of the <see cref="EncryptedMessage" /> class from a plain inner message data.
        /// </summary>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="salt">
        ///     Server Salt is a (random) 64-bit number periodically (say, every 24 hours) changed (separately for
        ///     each session) at the request of the server. All subsequent messages must contain the new salt (although, messages
        ///     with the old salt are still accepted for a further 300 seconds). Required to protect against replay attacks and
        ///     certain tricks associated with adjusting the client clock to a moment in the distant future.
        /// </param>
        /// <param name="sessionId">
        ///     Session is a (random) 64-bit number generated by the client to distinguish between individual sessions (for
        ///     example, between different instances of the application, created with the same authorization key). The session in
        ///     conjunction with the key identifier corresponds to an application instance. The server can maintain session state.
        ///     Under no circumstances can a message meant for one session be sent into a different session. The server may
        ///     unilaterally forget any client sessions; clients should be able to handle this.
        /// </param>
        /// <param name="messageId">
        ///     Message Identifier is a (time-dependent) 64-bit number used uniquely to identify a message within a session. Client
        ///     message identifiers are divisible by 4, server message identifiers modulo 4 yield 1 if the message is a response to
        ///     a client message, and 3 otherwise. Client message identifiers must increase monotonically (within a single
        ///     session), the same as server message identifiers, and must approximately equal unixtime*2^32. This way, a message
        ///     identifier points to the approximate moment in time the message was created. A message is rejected over 300 seconds
        ///     after it is created or 30 seconds before it is created (this is needed to protect from replay attacks). In this
        ///     situation, it must be re-sent with a different identifier (or placed in a container with a higher identifier). The
        ///     identifier of a message container must be strictly greater than those of its nested messages.
        /// </param>
        /// <param name="seqNumber">
        ///     Message Sequence Number is a 32-bit number equal to twice the number of “content-related” messages (those requiring
        ///     acknowledgment, and in particular those that are not containers) created by the sender prior to this message and
        ///     subsequently incremented by one if the current message is a content-related message. A container is always
        ///     generated after its entire contents; therefore, its sequence number is greater than or equal to the sequence
        ///     numbers of the messages contained in it.
        /// </param>
        /// <param name="messageData">Plain inner message data.</param>
        /// <param name="sender">Sender of the message.</param>
        /// <param name="hashServices">Hash services.</param>
        /// <param name="encryptionServices">Encryption services.</param>
        public EncryptedMessage([NotNull] byte[] authKey, ulong salt, ulong sessionId, ulong messageId, uint seqNumber, [NotNull] byte[] messageData, Sender sender,
            [NotNull] IHashServices hashServices, [NotNull] IEncryptionServices encryptionServices)
        {
            Argument.IsNotNull(() => authKey);
            Argument.IsNotNull(() => messageData);
            Argument.IsNotNull(() => hashServices);
            Argument.IsNotNull(() => encryptionServices);

            _authKeyId = ComputeAuthKeyId(authKey, hashServices);
            _salt = salt;
            _sessionId = sessionId;
            _messageId = messageId;
            _seqNumber = seqNumber;
            _messageData = (byte[]) messageData.Clone();

            _messageDataLength = _messageData.Length;
            int innerDataLength = InnerHeaderLength + _messageDataLength;
            int mod = innerDataLength%Alignment;
            int paddingLength = mod > 0 ? Alignment - mod : 0;
            int innerDataWithPaddingLength = innerDataLength + paddingLength;

            _length = OuterHeaderLength + innerDataWithPaddingLength;

            // Writing inner data.
            var innerDataWithPadding = new byte[innerDataWithPaddingLength];
            using (var streamer = new TLStreamer(innerDataWithPadding))
            {
                streamer.WriteUInt64(_salt);
                streamer.WriteUInt64(_sessionId);
                streamer.WriteUInt64(_messageId);
                streamer.WriteUInt32(_seqNumber);
                streamer.WriteInt32(_messageDataLength);
                streamer.Write(_messageData);
                streamer.WriteRandomData(paddingLength);
            }

            byte[] innerDataSHA1 = hashServices.ComputeSHA1(innerDataWithPadding, 0, innerDataLength);
            _msgKey = innerDataSHA1.ToInt128(innerDataSHA1.Length - 16, true);

            // Encrypting.
            byte[] aesKey, aesIV;
            ComputeAesKeyAndIV(authKey, _msgKey, out aesKey, out aesIV, hashServices, sender);
            byte[] encryptedData = encryptionServices.Aes256IgeEncrypt(innerDataWithPadding, aesKey, aesIV);

            Debug.Assert(encryptedData.Length == innerDataWithPaddingLength, "Wrong encrypted data length.");

            _messageBytes = new byte[_length];
            using (var streamer = new TLStreamer(_messageBytes))
            {
                // Writing header.
                streamer.WriteUInt64(_authKeyId);
                streamer.WriteInt128(_msgKey);

                // Writing encrypted data.
                streamer.Write(encryptedData, 0, innerDataWithPaddingLength);
            }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EncryptedMessage" /> class from a whole message bytes, which contain
        ///     encrypted data.
        /// </summary>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messageBytes">Whole message bytes, which contain encrypted data.</param>
        /// <param name="sender">Sender of the message.</param>
        /// <param name="hashServices">Hash services.</param>
        /// <param name="encryptionServices">Encryption services.</param>
        public EncryptedMessage([NotNull] byte[] authKey, [NotNull] byte[] messageBytes, Sender sender, [NotNull] IHashServices hashServices,
            [NotNull] IEncryptionServices encryptionServices)
        {
            Argument.IsNotNull(() => authKey);
            Argument.IsNotNull(() => messageBytes);
            Argument.IsNotNull(() => hashServices);
            Argument.IsNotNull(() => encryptionServices);

            ulong authKeyId = ComputeAuthKeyId(authKey, hashServices);
            _messageBytes = messageBytes;
            _length = _messageBytes.Length;

            var encryptedData = new byte[_length - OuterHeaderLength];

            using (var streamer = new TLStreamer(_messageBytes))
            {
                // Reading header.
                _authKeyId = streamer.ReadUInt64();
                if (_authKeyId != authKeyId)
                {
                    throw new InvalidAuthKey(string.Format("Message encrypted with auth key with id={0}, but auth key provided for decryption with id={1}.", _authKeyId,
                        authKeyId));
                }
                _msgKey = streamer.ReadInt128();

                // Reading encrypted data.
                streamer.Read(encryptedData, 0, encryptedData.Length);
            }

            // Decrypting.
            byte[] aesKey, aesIV;
            ComputeAesKeyAndIV(authKey, _msgKey, out aesKey, out aesIV, hashServices, sender);
            byte[] innerDataWithPadding = encryptionServices.Aes256IgeDecrypt(encryptedData, aesKey, aesIV);

            using (var streamer = new TLStreamer(innerDataWithPadding))
            {
                _salt = streamer.ReadUInt64();
                _sessionId = streamer.ReadUInt64();
                _messageId = streamer.ReadUInt64();
                _seqNumber = streamer.ReadUInt32();
                _messageDataLength = streamer.ReadInt32();
                _messageData = streamer.ReadBytes(_messageDataLength);
            }
        }

        /// <summary>
        ///     Server Salt is a (random) 64-bit number periodically (say, every 24 hours) changed (separately for each session) at
        ///     the request of the server. All subsequent messages must contain the new salt (although, messages with the old salt
        ///     are still accepted for a further 300 seconds). Required to protect against replay attacks and certain tricks
        ///     associated with adjusting the client clock to a moment in the distant future.
        /// </summary>
        public ulong Salt
        {
            get { return _salt; }
        }

        /// <summary>
        ///     Session is a (random) 64-bit number generated by the client to distinguish between individual sessions (for
        ///     example, between different instances of the application, created with the same authorization key). The session in
        ///     conjunction with the key identifier corresponds to an application instance. The server can maintain session state.
        ///     Under no circumstances can a message meant for one session be sent into a different session. The server may
        ///     unilaterally forget any client sessions; clients should be able to handle this.
        /// </summary>
        public ulong SessionId
        {
            get { return _sessionId; }
        }

        /// <summary>
        ///     Message Sequence Number is a 32-bit number equal to twice the number of “content-related” messages (those requiring
        ///     acknowledgment, and in particular those that are not containers) created by the sender prior to this message and
        ///     subsequently incremented by one if the current message is a content-related message. A container is always
        ///     generated after its entire contents; therefore, its sequence number is greater than or equal to the sequence
        ///     numbers of the messages contained in it.
        /// </summary>
        public uint SeqNumber
        {
            get { return _seqNumber; }
        }

        /// <summary>
        ///     Plain inner message data.
        /// </summary>
        public byte[] MessageData
        {
            get { return _messageData; }
        }

        /// <summary>
        ///     Key Identifier is the 64 lower-order bits of the SHA1 hash of the authorization key are used to indicate which
        ///     particular key was used to encrypt a message. Keys must be uniquely defined by the 64 lower-order bits of their
        ///     SHA1, and in the event of a collision, an authorization key is regenerated. A zero key identifier means that
        ///     encryption is not used which is permissible for a limited set of message types used during registration to generate
        ///     an authorization key based on a Diffie-Hellman exchange.
        /// </summary>
        public ulong AuthKeyId
        {
            get { return _authKeyId; }
        }

        /// <summary>
        ///     Message Key is the lower-order 128 bits of the SHA1 hash of the part of the message to be encrypted (including the
        ///     internal header and excluding the alignment bytes).
        /// </summary>
        public Int128 MsgKey
        {
            get { return _msgKey; }
        }

        /// <summary>
        ///     Message Identifier is a (time-dependent) 64-bit number used uniquely to identify a message within a session. Client
        ///     message identifiers are divisible by 4, server message identifiers modulo 4 yield 1 if the message is a response to
        ///     a client message, and 3 otherwise. Client message identifiers must increase monotonically (within a single
        ///     session), the same as server message identifiers, and must approximately equal unixtime*2^32. This way, a message
        ///     identifier points to the approximate moment in time the message was created. A message is rejected over 300 seconds
        ///     after it is created or 30 seconds before it is created (this is needed to protect from replay attacks). In this
        ///     situation, it must be re-sent with a different identifier (or placed in a container with a higher identifier). The
        ///     identifier of a message container must be strictly greater than those of its nested messages.
        /// </summary>
        public ulong MessageId
        {
            get { return _messageId; }
        }

        /// <summary>
        ///     Message inner data length.
        /// </summary>
        public int MessageDataLength
        {
            get { return _messageDataLength; }
        }

        /// <summary>
        ///     Whole message length.
        /// </summary>
        public int Length
        {
            get { return _length; }
        }

        /// <summary>
        ///     Whole message bytes.
        /// </summary>
        public byte[] MessageBytes
        {
            get { return _messageBytes; }
        }

        private ulong ComputeAuthKeyId(byte[] authKey, IHashServices hashServices)
        {
            byte[] authKeySHA1 = hashServices.ComputeSHA1(authKey);
            return authKeySHA1.ToUInt64(authKeySHA1.Length - 8, true);
        }

        private static void ComputeAesKeyAndIV(byte[] authKey, Int128 msgKey, out byte[] aesKey, out byte[] aesIV, IHashServices hashServices, Sender sender)
        {
            // x = 0 for messages from client to server and x = 8 for those from server to client.
            int x;
            switch (sender)
            {
                case Sender.Client:
                    x = 0;
                    break;
                case Sender.Server:
                    x = 8;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("sender");
            }

            byte[] msgKeyBytes = msgKey.ToBytes();

            byte[] buffer = _aesKeyAndIVComputationBuffer ?? (_aesKeyAndIVComputationBuffer = new byte[32 + MsgKeyLength]);

            // sha1_a = SHA1 (msg_key + substr (auth_key, x, 32));
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 0, MsgKeyLength);
            Buffer.BlockCopy(authKey, x, buffer, MsgKeyLength, 32);
            byte[] sha1A = hashServices.ComputeSHA1(buffer);

            // sha1_b = SHA1 (substr (auth_key, 32+x, 16) + msg_key + substr (auth_key, 48+x, 16));
            Buffer.BlockCopy(authKey, 32 + x, buffer, 0, 16);
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 16, MsgKeyLength);
            Buffer.BlockCopy(authKey, 48 + x, buffer, 16 + MsgKeyLength, 16);
            byte[] sha1B = hashServices.ComputeSHA1(buffer);

            // sha1_с = SHA1 (substr (auth_key, 64+x, 32) + msg_key);
            Buffer.BlockCopy(authKey, 64 + x, buffer, 0, 32);
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 32, MsgKeyLength);
            byte[] sha1C = hashServices.ComputeSHA1(buffer);

            // sha1_d = SHA1 (msg_key + substr (auth_key, 96+x, 32));
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 0, MsgKeyLength);
            Buffer.BlockCopy(authKey, 96 + x, buffer, MsgKeyLength, 32);
            byte[] sha1D = hashServices.ComputeSHA1(buffer);

            // aes_key = substr (sha1_a, 0, 8) + substr (sha1_b, 8, 12) + substr (sha1_c, 4, 12);
            aesKey = new byte[32];
            Buffer.BlockCopy(sha1A, 0, aesKey, 0, 8);
            Buffer.BlockCopy(sha1B, 8, aesKey, 8, 12);
            Buffer.BlockCopy(sha1C, 4, aesKey, 20, 12);

            // aes_iv = substr (sha1_a, 8, 12) + substr (sha1_b, 0, 8) + substr (sha1_c, 16, 4) + substr (sha1_d, 0, 8);
            aesIV = new byte[32];
            Buffer.BlockCopy(sha1A, 8, aesIV, 0, 12);
            Buffer.BlockCopy(sha1B, 0, aesIV, 12, 8);
            Buffer.BlockCopy(sha1C, 16, aesIV, 20, 4);
            Buffer.BlockCopy(sha1D, 0, aesIV, 24, 8);
        }
    }
}