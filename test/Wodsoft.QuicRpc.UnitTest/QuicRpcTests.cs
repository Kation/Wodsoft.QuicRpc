using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Net.Quic;
using System.Net.Security;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Wodsoft.QuicRpc.UnitTest
{
    public abstract class QuicRpcTests
    {
        private readonly CertificateContext _context;

        public QuicRpcTests(CertificateContext context)
        {
            _context = context;
        }

        protected async Task<ConnectionContext> GetConnectionContextAsync()
        {
            var listener = await QuicListener.ListenAsync(new QuicListenerOptions
            {
                ApplicationProtocols = [SslApplicationProtocol.Http3],
                ConnectionOptionsCallback = (connection, helloInfo, _) =>
                {
                    QuicServerConnectionOptions options = new QuicServerConnectionOptions
                    {
                        ServerAuthenticationOptions = new SslServerAuthenticationOptions
                        {
                            ServerCertificate = _context.ServerCertificate,
                            ClientCertificateRequired = true,
                            RemoteCertificateValidationCallback = (_, _, _, _) =>
                            {
                                return true;
                            },
                            ApplicationProtocols = [SslApplicationProtocol.Http3]
                        },
                        DefaultCloseErrorCode = 0,
                        DefaultStreamErrorCode = 0,
                        MaxInboundBidirectionalStreams = 1024,
                        MaxInboundUnidirectionalStreams = 128,
                        IdleTimeout = TimeSpan.FromMinutes(10),
                        KeepAliveInterval = TimeSpan.FromMinutes(1)
                    };
                    return ValueTask.FromResult(options);
                },
                ListenEndPoint = new IPEndPoint(IPAddress.Loopback, 0)
            });

            var acceptTask = listener.AcceptConnectionAsync();

            var clientConnection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
            {
                RemoteEndPoint = listener.LocalEndPoint,
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    ClientCertificates = [_context.ClientCertificate],
                    RemoteCertificateValidationCallback = (_, _, _, _) =>
                    {
                        return true;
                    },
                    ApplicationProtocols = [SslApplicationProtocol.Http3],
                    TargetHost = "localhost"
                },
                DefaultCloseErrorCode = 0,
                DefaultStreamErrorCode = 0,
                MaxInboundBidirectionalStreams = 1024,
                MaxInboundUnidirectionalStreams = 128,
                IdleTimeout = TimeSpan.FromMinutes(10),
                KeepAliveInterval = TimeSpan.FromMinutes(1)
            });

            var serverConnection = await acceptTask;

            return new ConnectionContext(listener, clientConnection, serverConnection);
        }

        protected class ConnectionContext : IAsyncDisposable
        {
            private bool _disposed;

            public ConnectionContext(QuicListener listener, QuicConnection serverConnection, QuicConnection clientConnection)
            {
                Listener = listener;
                ServerConnection = serverConnection;
                ClientConnection = clientConnection;
            }

            public QuicListener Listener { get; }

            public QuicConnection ServerConnection { get; }

            public QuicConnection ClientConnection { get; }

            public async ValueTask DisposeAsync()
            {
                if (_disposed)
                    return;
                await ClientConnection.DisposeAsync();
                await ServerConnection.DisposeAsync();
                await Listener.DisposeAsync();
            }
        }

        protected class PipeStreamPool : IAsyncDisposable
        {
            private readonly ConcurrentQueue<Stream> _queue;

            public PipeStreamPool()
            {
                _queue = new ConcurrentQueue<Stream>();
            }

            public async ValueTask<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                if (_queue.TryDequeue(out var stream))
                    return stream;
                var clientStream = new NamedPipeClientStream("quicrpc");
                await clientStream.ConnectAsync();
                return clientStream;
            }

            public void Return(Stream stream)
            {
                _queue.Enqueue(stream);
            }

            public async ValueTask DisposeAsync()
            {
                while (_queue.TryDequeue(out var stream))
                    await stream.DisposeAsync();
            }
        }
    }
}
