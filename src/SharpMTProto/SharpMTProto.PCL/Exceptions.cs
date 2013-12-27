// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Exceptions.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMTProto
{
    public class MTProtoException : Exception
    {
        public MTProtoException()
        {
        }

        public MTProtoException(string message) : base(message)
        {
        }

        public MTProtoException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class WrongResponseException : MTProtoException
    {
        public WrongResponseException()
        {
        }

        public WrongResponseException(string message) : base(message)
        {
        }

        public WrongResponseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class InvalidMessageException : MTProtoException
    {
        public InvalidMessageException()
        {
        }

        public InvalidMessageException(string message) : base(message)
        {
        }

        public InvalidMessageException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class CouldNotConnectException : MTProtoException
    {
        public CouldNotConnectException()
        {
        }

        public CouldNotConnectException(string message) : base(message)
        {
        }

        public CouldNotConnectException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class PublicKeyNotFoundException : MTProtoException
    {
        public PublicKeyNotFoundException(ulong fingerprint) : base(string.Format("Public key with fingerprint {0:X16} not found.", fingerprint))
        {
        }

        public PublicKeyNotFoundException(IEnumerable<ulong> fingerprints) : base(GetMessage(fingerprints))
        {
        }

        private static string GetMessage(IEnumerable<ulong> fingerprints)
        {
            var sb = new StringBuilder();
            sb.Append("There are no keys found with corresponding fingerprints: ");
            foreach (var fingerprint in fingerprints)
            {
                sb.Append(string.Format("0x{0:X16}", fingerprint));
            }
            sb.Append(".");
            return sb.ToString();
        }
    }
}
