// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoClient.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
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
        private IMTProtoConnection _connection;
        private bool _isDisposed;

        public MTProtoClient([NotNull] IMTProtoConnection connection)
        {
            Argument.IsNotNull(() => connection);

            _connection = connection;
        }

        public async Task<byte[]> CreateAuthKey(Int128 nonce)
        {
            ThrowIfDisposed();

            Log.Info(string.Format("Creating auth key with nonce 0x{0:X}.", nonce));
            try
            {
                await TryConnectIfDisconnected();
                var resPQ = await _connection.ReqPqAsync(new ReqPqArgs {Nonce = nonce}) as ResPQ;
                if (resPQ == null)
                {
                    throw new WrongResponseException();
                }
                if (resPQ.Nonce != nonce)
                {
                    throw new WrongResponseException(string.Format("Nonce in response ({0}) differs from the nonce in request ({1}).", resPQ.Nonce, nonce));
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not create auth key.");
            }
            return null;
        }

        private async Task TryConnectIfDisconnected()
        {
            if (!_connection.IsConnected)
            {
                MTProtoConnectResult result = await _connection.Connect();
                if (result != MTProtoConnectResult.Success)
                {
                    throw new CouldNotConnectException("Connection trial was unsuccessful.");
                }
            }
        }

        #region Disposable
        public void Dispose()
        {
            Dispose(true);
        }

        protected void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName, "Can not access disposed client.");
            }
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
                if (_connection != null)
                {
                    _connection.Dispose();
                    _connection = null;
                }
            }
        }
        #endregion
    }
}
