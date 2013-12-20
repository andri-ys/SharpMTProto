// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestsSetup.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using Catel.Logging;
using NUnit.Framework;

namespace SharpMTProto.Tests
{
    [SetUpFixture]
    public class TestsSetup
    {
        [SetUp]
        public void Initialize()
        {
            LogManager.AddDebugListener(true);
        }
    }
}