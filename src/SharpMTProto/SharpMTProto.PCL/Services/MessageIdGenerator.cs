// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageIdGenerator.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using SharpMTProto.Utils;

namespace SharpMTProto.Services
{
    /// <summary>
    ///     Interface for a message ID generator.
    /// </summary>
    public interface IMessageIdGenerator
    {
        ulong GetNextMessageId();
    }

    /// <summary>
    ///     The default MTProto message ID generator.
    /// </summary>
    public class MessageIdGenerator : IMessageIdGenerator
    {
        private ulong _lastMessageId;

        public ulong GetNextMessageId()
        {
            ulong messageId = UnixTimeUtils.GetCurrentUnixTimestampSeconds();
            messageId <<= 32;
            messageId -= messageId%4;
            if (messageId == _lastMessageId)
            {
                messageId += 4;
            }
            _lastMessageId = messageId;
            return messageId;
        }
    }
}
