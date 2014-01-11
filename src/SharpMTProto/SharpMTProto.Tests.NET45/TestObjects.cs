// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestObjects.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using SharpTL;

namespace SharpMTProto.Tests
{
    [TLObject(0x100500)]
    public class TestRequest
    {
        [TLProperty(1)]
        public int TestId { get; set; }
    }

    [TLObject(0x500100)]
    public class TestResponse
    {
        [TLProperty(1)]
        public int TestId { get; set; }

        [TLProperty(2)]
        public string TestText { get; set; }
    }
}
