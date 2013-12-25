// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestData.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using BigMath.Utils;
using SharpTL;

namespace SharpMTProto.Tests
{
    public static class TestData
    {
        public static readonly byte[] ReqPQBytes = "00000000000000004A967027C47AE55114000000789746603E0549828CCA27E966B301A48FECE2FC".ToBytes();

        public static readonly byte[] ResPQBytes =
            "000000000000000001C8831EC97AE55140000000632416053E0549828CCA27E966B301A48FECE2FCA5CF4D33F4A11EA877BA4AA5739073300817ED48941A08F98100000015C4B51C01000000216BE86C022BB4C3"
                .ToBytes();

        public static readonly byte[] AuthKey =
            "AB96E207C631300986F30EF97DF55E179E63C112675F0CE502EE76D74BBEE6CBD1E95772818881E9F2FF54BD52C258787474F6A7BEA61EABE49D1D01D55F64FC07BC31685716EC8FB46FEACF9502E42CFD6B9F45A08E90AA5C2B5933AC767CBE1CD50D8E64F89727CA4A1A5D32C0DB80A9FCDBDDD4F8D5A1E774198F1A4299F927C484FEEC395F29647E43C3243986F93609E23538C21871DF50E00070B3B6A8FA9BC15628E8B43FF977409A61CEEC5A21CF7DFB5A4CC28F5257BC30CD8F2FB92FBF21E28924065F50E0BBD5E11A420300E2C136B80E9826C6C5609B5371B7850AA628323B6422F3A94F6DFDE4C3DC1EA60F7E11EE63122B3F39CBD1A8430157"
                .ToBytes();

        public static readonly IMessageIdGenerator MessageIdGenerator = new TestMessageIdsGenerator();
        public static readonly INonceGenerator NonceGenerator = new TestNonceGenerator();

        private class TestMessageIdsGenerator : IMessageIdGenerator
        {
            private static readonly ulong[] MessageIds = {0x51e57ac42770964aUL};
            private int _mi;

            public ulong GetNextMessageId()
            {
                return MessageIds[_mi++];
            }
        }

        private class TestNonceGenerator : INonceGenerator
        {
            private static readonly byte[] Nonces = ("3E0549828CCA27E966B301A48FECE2FC" + "311C85DB234AA2640AFC4A76A735CF5B1F0FD68BD17FA181E1229AD867CC024D").ToBytes();
            private readonly TLStreamer _nonceStream = new TLStreamer(Nonces);

            public byte[] GetNonce(uint length)
            {
                return _nonceStream.ReadBytes((int) length);
            }
        }
    }
}
