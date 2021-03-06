﻿using System;
using System.Net;
using SimpleSocket.Logging;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using SimpleSocket.Events;

namespace SimpleSocket
{
    enum TcpSecurityType
    {
        Normal,
        Secure
    }

    public class TcpService<TFramer> where TFramer: IMessageFramer, new()
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpService<TFramer>>();

        private readonly IPEndPoint _serverEndPoint;
        private readonly TcpServerListener _serverListener;
        private readonly TcpSecurityType _securityType;
        private readonly X509Certificate _certificate;

        public event EventHandler<ConnectionEstablishedEventArgs> ConnectionEstablished;
        public event EventHandler<FramedMessageArrivedEventArgs> MessageArrived;
        public event EventHandler<ConnectionClosedEventArgs> ConnectionClosed;


        public TcpService(IPEndPoint serverEndPoint, X509Certificate certificate)
        {
            Ensure.NotNull(serverEndPoint, nameof(serverEndPoint));

            _serverEndPoint = serverEndPoint;
            _serverListener = new TcpServerListener(_serverEndPoint);
            _certificate = certificate;
            _securityType = certificate == null ? TcpSecurityType.Normal : TcpSecurityType.Secure;
        }

        public void Start()
        {
            try
            {
                _serverListener.StartListening(OnConnectionAccepted, _securityType.ToString());
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "Could not start listen using the {0} binding {1}:{2}.", _securityType, _serverEndPoint.Address, _serverEndPoint.Port);
                throw;
            }
        }


        public void Stop()
        {
            _serverListener.Stop();
        }

        private void OnConnectionAccepted(IPEndPoint endPoint, Socket socket)
        {
            var conn = _securityType == TcpSecurityType.Secure
                ? TcpConnectionSsl.CreateServerFromSocket(Guid.NewGuid(), endPoint, socket, _certificate, verbose: true)
                : TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(), endPoint, socket, verbose: true);
            Log.Info("TCP connection accepted: [{0}, {1}, L{2}, {3:B}].", _securityType, conn.RemoteEndPoint, conn.LocalEndPoint, conn.ConnectionId);

            var manager = new TcpConnectionManager(
                    string.Format("{0}", _securityType.ToString().ToLower()),
                    conn,
                    new TFramer(),
                    (m, d) => MessageArrived(this, new FramedMessageArrivedEventArgs(m, d)),
                    (m, e) => ConnectionClosed(this, new ConnectionClosedEventArgs(m, e)));

            ConnectionEstablished(this, new ConnectionEstablishedEventArgs(manager));
            manager.StartReceiving();
        }
    }


}

