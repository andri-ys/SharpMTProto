// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestData.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using BigMath;
using MTProtoSchema;
using SharpTL;

namespace SharpMTProto.Tests
{
    public static class TestData
    {
        public static readonly Int128 Nonce = Int128.Parse("0x3E0549828CCA27E966B301A48FECE2FC");
        public static readonly Int128 ServerNonce = Int128.Parse("0xA5CF4D33F4A11EA877BA4AA573907330");
        
        public static readonly byte[] ResPQBytes = TLRig.Default.Serialize(ResPQ);
        public static readonly byte[] ReqPQBytes = TLRig.Default.Serialize(ReqPQ);
   
        public static resPQ ResPQ
        {
            get
            {
                return new resPQ
                {
                    nonce = Nonce,
                    server_nonce = ServerNonce,
                    pq = new byte[] {0x17, 0xED, 0x48, 0x94, 0x1A, 0x08, 0xF9, 0x81},
                    server_public_key_fingerprints = new List<ulong> {0xc3b42b026ce86b21}
                };
            }
        }

        public static req_pq ReqPQ
        {
            get { return new req_pq {nonce = Nonce}; }
        }
    }
}
