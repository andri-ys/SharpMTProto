// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IMessage.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    public interface IMessage
    {
        byte[] MessageBytes { get; }
    }
}
