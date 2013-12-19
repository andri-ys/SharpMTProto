// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IMTProtoProxy.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using MTProtoSchema;

namespace SharpMTProto
{
    /// <summary>
    ///     Interface for a MTProto proxy which implements <see cref="ITLMethods" />.
    /// </summary>
    public interface IMTProtoProxy : ITLMethods, IDisposable
    {
        IMTProtoConnection Connection { get; }
    }
}
