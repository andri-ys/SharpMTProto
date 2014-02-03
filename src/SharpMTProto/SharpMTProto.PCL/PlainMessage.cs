// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PlainMessage.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using Catel;
using SharpMTProto.Annotations;
using SharpTL;

namespace SharpMTProto
{
    /// <summary>
    ///     Plain (unencrypted) message.
    /// </summary>
    public class PlainMessage : IMessage
    {
        /// <summary>
        ///     Message header length in bytes.
        /// </summary>
        public const int HeaderLength = 20;

        private readonly int _dataLength;
        private readonly int _length;
        private readonly byte[] _messageBytes;
        private readonly byte[] _messageData;
        private readonly ulong _messageId;

        public PlainMessage(ulong messageId, [NotNull] byte[] messageData)
        {
            Argument.IsNotNull(() => messageData);

            int dataLength = messageData.Length;
            _messageId = messageId;
            _messageData = messageData;
            _dataLength = dataLength;
            _length = HeaderLength + dataLength;
            _messageBytes = new byte[_length];

            using (var streamer = new TLStreamer(_messageBytes))
            {
                // Writing header.
                streamer.WriteInt64(0); // Plain unencrypted message must always have zero auth key id.
                streamer.WriteUInt64(_messageId);
                streamer.WriteInt32(_dataLength);

                // Writing data.
                streamer.Write(messageData, 0, _dataLength);
            }
        }

        public ulong MessageId
        {
            get { return _messageId; }
        }

        public int DataLength
        {
            get { return _dataLength; }
        }

        public int Length
        {
            get { return _length; }
        }

        public byte[] MessageBytes
        {
            get { return _messageBytes; }
        }

        public byte[] MessageData
        {
            get { return _messageData; }
        }
    }
}
