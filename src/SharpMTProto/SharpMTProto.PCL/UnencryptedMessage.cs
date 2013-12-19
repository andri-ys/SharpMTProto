// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UnencryptedMessage.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using Catel;
using SharpMTProto.Annotations;
using SharpTL;

namespace SharpMTProto
{
    /// <summary>
    ///     Unencrypted message.
    /// </summary>
    public class UnencryptedMessage : IMessage
    {
        /// <summary>
        ///     Message header length in bytes.
        /// </summary>
        public const int HeaderLength = 20;

        private readonly byte[] _messageBytes;
        private readonly int _dataLength;
        private readonly ulong _messageId;
        private readonly int _length;

        public UnencryptedMessage(ulong messageId, [NotNull] byte[] messageData)
        {
            Argument.IsNotNull(() => messageData);

            int dataLength = messageData.Length;
            _messageId = messageId;
            _dataLength = dataLength;
            _length = HeaderLength + dataLength;
            _messageBytes = new byte[_length];

            using (var streamer = new TLStreamer(MessageBytes))
            {
                // Writing header.
                streamer.WriteLong(0); // Unencrypted message must always have zero auth key id.
                streamer.WriteULong(MessageId);
                streamer.WriteInt(DataLength);

                // Writing data.
                streamer.Write(messageData, 0, DataLength);
            }
        }

        public ulong MessageId
        {
            get { return _messageId; }
        }

        public int Length
        {
            get { return _length; }
        }

        public int DataLength
        {
            get { return _dataLength; }
        }

        public byte[] MessageBytes
        {
            get { return _messageBytes; }
        }

        public byte[] GetMessageData()
        {
            var messageData = new byte[DataLength];
            Buffer.BlockCopy(_messageBytes, HeaderLength, messageData, 0, messageData.Length);
            return messageData;
        }
    }
}
