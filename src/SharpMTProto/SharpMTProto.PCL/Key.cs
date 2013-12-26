using BigMath.Utils;
using SharpTL;

namespace SharpMTProto
{
    /// <summary>
    ///     Contains public key, exponent and fingerprint.
    /// </summary>
    [TLObject(0xEED4C70BU)]
    public class Key
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Key"/> class.
        /// </summary>
        /// <param name="publicKey">Public key as a HEX string.</param>
        /// <param name="exponent">Exponent as a HEX string.</param>
        /// <param name="fingerprint">Fingerprint.</param>
        public Key(string publicKey, string exponent, ulong fingerprint) : this((byte[]) publicKey.ToBytes(), exponent.ToBytes(), fingerprint)
        {
        }

        public Key(byte[] publicKey, byte[] exponent, ulong fingerprint)
        {
            PublicKey = publicKey;
            Exponent = exponent;
            Fingerprint = fingerprint;
        }

        [TLProperty(1)]
        public byte[] PublicKey { get; set; }

        [TLProperty(2)]
        public byte[] Exponent { get; set; }

        /// <summary>
        ///     Represents lower 64 bits of the SHA1(PublicKey).
        /// </summary>
        public ulong Fingerprint { get; set; }
    }
}