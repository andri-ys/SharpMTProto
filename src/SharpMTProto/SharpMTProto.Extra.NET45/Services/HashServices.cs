﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HashServices.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Security.Cryptography;

namespace SharpMTProto.Services
{
    public class HashServices : IHashServices
    {
        public byte[] ComputeSHA1(byte[] data)
        {
            return ComputeSHA1(data, 0, data.Length);
        }

        public byte[] ComputeSHA1(byte[] data, int offset, int count)
        {
            using (var sha = new SHA1CryptoServiceProvider())
            {
                return sha.ComputeHash(data, offset, count);
            }
        }

        public byte[] ComputeSHA1(ArraySegment<byte> data)
        {
            return ComputeSHA1(data.Array, data.Offset, data.Count);
        }

        public byte[] ComputeSHA1(Stream stream)
        {
            using (var sha = new SHA1CryptoServiceProvider())
            {
                return sha.ComputeHash(stream);
            }
        }
    }
}
