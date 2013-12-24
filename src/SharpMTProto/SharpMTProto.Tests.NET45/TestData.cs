// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestData.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using BigMath;
using BigMath.Utils;
using MTProtoSchema;

namespace SharpMTProto.Tests
{
    public static class TestData
    {
        public const ulong MessageId1 = 0x51e57ac42770964a;
        public static readonly Int128 Nonce = Int128.Parse("0xFCE2EC8FA401B366E927CA8C8249053E");
        public static readonly Int128 ServerNonce = Int128.Parse("0xA5CF4D33F4A11EA877BA4AA573907330");

        public static readonly byte[] ReqPQBytes = "00000000000000004A967027C47AE55114000000789746603E0549828CCA27E966B301A48FECE2FC".ToBytes();

        public static readonly byte[] ResPQBytes =
            "000000000000000001C8831EC97AE55140000000632416053E0549828CCA27E966B301A48FECE2FCA5CF4D33F4A11EA877BA4AA5739073300817ED48941A08F98100000015C4B51C01000000216BE86C022BB4C3"
                .ToBytes();

        public static readonly byte[] AuthKey =
            "AB96E207C631300986F30EF97DF55E179E63C112675F0CE502EE76D74BBEE6CBD1E95772818881E9F2FF54BD52C258787474F6A7BEA61EABE49D1D01D55F64FC07BC31685716EC8FB46FEACF9502E42CFD6B9F45A08E90AA5C2B5933AC767CBE1CD50D8E64F89727CA4A1A5D32C0DB80A9FCDBDDD4F8D5A1E774198F1A4299F927C484FEEC395F29647E43C3243986F93609E23538C21871DF50E00070B3B6A8FA9BC15628E8B43FF977409A61CEEC5A21CF7DFB5A4CC28F5257BC30CD8F2FB92FBF21E28924065F50E0BBD5E11A420300E2C136B80E9826C6C5609B5371B7850AA628323B6422F3A94F6DFDE4C3DC1EA60F7E11EE63122B3F39CBD1A8430157"
                .ToBytes();

        public static ResPQ ResPQ
        {
            get
            {
                return new ResPQ
                {
                    Nonce = Nonce,
                    ServerNonce = ServerNonce,
                    Pq = new byte[] {0x17, 0xED, 0x48, 0x94, 0x1A, 0x08, 0xF9, 0x81},
                    ServerPublicKeyFingerprints = new List<ulong> {0xc3b42b026ce86b21UL}
                };
            }
        }

        public static ReqPqArgs ReqPQ
        {
            get { return new ReqPqArgs {Nonce = Nonce}; }
        }
    }
}
