// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClient.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using BigMath;
using Catel;
using Catel.Logging;
using MTProtoSchema;
using SharpMTProto.Annotations;

namespace SharpMTProto
{
    /// <summary>
    ///     MTProto client.
    /// </summary>
    [UsedImplicitly]
    public class MTProtoClient : IDisposable
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private bool _isDisposed;
        private IMTProtoProxy _mtProtoProxy;

        public MTProtoClient([NotNull] IMTProtoProxy mtProtoProxy)
        {
            Argument.IsNotNull(() => mtProtoProxy);

            _mtProtoProxy = mtProtoProxy;
        }

        public void CreateAuthKey(Int128 nonce)
        {
            Log.Info(string.Format("Creating auth key with nonce 0x{0:X}.", nonce));
            try
            {
                var resPQ = _mtProtoProxy.req_pq(new req_pq {nonce = nonce}) as resPQ;
            }
            catch (MTProtoException e)
            {
                Log.Error(e);
            }
        }

        #region Disposable
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (isDisposing)
            {
                if (_mtProtoProxy != null)
                {
                    _mtProtoProxy.Dispose();
                    _mtProtoProxy = null;
                }
            }
        }
        #endregion
    }
}
