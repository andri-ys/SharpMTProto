// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoProxy.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Reflection;
using Catel;
using MTProtoSchema;
using SharpMTProto.Annotations;
using SharpTL;

namespace SharpMTProto
{
    /// <summary>
    ///     MTProto proxy.
    /// </summary>
    [UsedImplicitly]
    public class MTProtoProxy : IMTProtoProxy
    {
        private readonly TLRig _tlRig;
        private IMTProtoConnection _connection;
        private bool _isDisposed;

        /// <summary>
        ///     Creates a new instance of the <see cref="MTProtoProxy" /> class.
        /// </summary>
        /// <param name="connectionManager"></param>
        /// <param name="tlRig"></param>
        /// <exception cref="System.ArgumentNullException">The <paramref name="tlRig" /> is <c>null</c>.</exception>
        /// <exception cref="System.ArgumentNullException">The <paramref name="connectionManager" /> is <c>null</c>.</exception>
        public MTProtoProxy([NotNull] IMTProtoConnectionManager connectionManager, [NotNull] TLRig tlRig)
        {
            Argument.IsNotNull(() => connectionManager);
            Argument.IsNotNull(() => tlRig);

            _tlRig = tlRig;
            _tlRig.PrepareSerializersForAllTLObjectsInAssembly(typeof (ITLMethods).GetTypeInfo().Assembly);

            _connection = connectionManager.CreateConnection();
        }

        /// <summary>
        ///     Request pq.
        /// </summary>
        /// <returns>Response with pq.</returns>
        public ResPQ req_pq(req_pq args)
        {
            _connection.SendUnencryptedMessage(_tlRig.Serialize(args));
            byte[] data = _connection.ReceiveUnencryptedMessage();

            var resPq = _tlRig.Deserialize(data) as resPQ;
            if (resPq == null)
            {
                throw new WrongResponseException();
            }
            if (resPq.nonce != args.nonce)
            {
                throw new WrongResponseException(string.Format("Nonce in response ({0}) differs from the nonce in request ({1}).", resPq.nonce, args.nonce));
            }

            return resPq;
        }

        public Server_DH_Params req_DH_params(req_DH_params args)
        {
            throw new NotImplementedException();
        }

        public Set_client_DH_params_answer set_client_DH_params(set_client_DH_params args)
        {
            throw new NotImplementedException();
        }

        public RpcDropAnswer rpc_drop_answer(rpc_drop_answer args)
        {
            throw new NotImplementedException();
        }

        public FutureSalts get_future_salts(get_future_salts args)
        {
            throw new NotImplementedException();
        }

        public Pong ping(ping args)
        {
            throw new NotImplementedException();
        }

        public Pong ping_delay_disconnect(ping_delay_disconnect args)
        {
            throw new NotImplementedException();
        }

        public DestroySessionRes destroy_session(destroy_session args)
        {
            throw new NotImplementedException();
        }

        public void http_wait(http_wait args)
        {
            throw new NotImplementedException();
        }

        public IMTProtoConnection Connection
        {
            get { return _connection; }
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
                if (_connection != null)
                {
                    _connection.Close();
                    _connection.Dispose();
                    _connection = null;
                }
            }
        }
        #endregion
    }
}
