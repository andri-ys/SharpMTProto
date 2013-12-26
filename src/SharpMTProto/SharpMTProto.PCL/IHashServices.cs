// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IHashServices.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace SharpMTProto
{
    public interface IHashServices
    {
        byte[] ComputeSHA1(byte[] data);
        byte[] ComputeSHA1(byte[] data, int offset, int count);
    }
}