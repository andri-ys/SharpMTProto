// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClientFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using BigMath;
using MTProtoSchema;
using NUnit.Framework;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class MTProtoClientFacts
    {
        [Test]
        public void Should_create_auth_key()
        {
            var client = new MTProtoClient();
            client.req_pq(new req_pq {nonce = Int128.Parse("0x3E0549828CCA27E966B301A48FECE2FC")});
        }
    }
}
