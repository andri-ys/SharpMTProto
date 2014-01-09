// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ITransportConfigProvider.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace SharpMTProto.Transport
{
    public interface ITransportConfigProvider
    {
        TransportConfig DefaultTransportConfig { get; } 
    }
}