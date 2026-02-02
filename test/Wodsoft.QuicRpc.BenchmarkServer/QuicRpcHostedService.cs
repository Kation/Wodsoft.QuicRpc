
using System.Net.Quic;
using System.Net.Security;
using System.Net;
using System.Security.Authentication;

#pragma warning disable CA1416 // 验证平台兼容性
namespace Wodsoft.QuicRpc.BenchmarkServer
{
    public class QuicRpcHostedService : IHostedService
    {
        private QuicRpcService<BenchmarkRpcContext> _quicRpcService;
        private CancellationTokenSource? _cts;
        private QuicListener? _listener;
        private Task? _serverTask;

        public QuicRpcHostedService()
        {
            _quicRpcService = new QuicRpcService<BenchmarkRpcContext>();
            _quicRpcService.BindFunctions(new BenchmarkRpcFunctions());
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = new CancellationTokenSource();
            _listener = await QuicListener.ListenAsync(new QuicListenerOptions
            {
                ApplicationProtocols = [SslApplicationProtocol.Http3],
                ConnectionOptionsCallback = (connection, helloInfo, _) =>
                {
                    QuicServerConnectionOptions options = new QuicServerConnectionOptions
                    {
                        ServerAuthenticationOptions = new SslServerAuthenticationOptions
                        {
                            ServerCertificate = CertificateHelper.ServerCertificate,
                            ApplicationProtocols = [SslApplicationProtocol.Http3],
                            EnabledSslProtocols = SslProtocols.Tls13
                        },
                        DefaultCloseErrorCode = 0,
                        DefaultStreamErrorCode = 0,
                        MaxInboundBidirectionalStreams = 1024,
                        MaxInboundUnidirectionalStreams = 128,
                        IdleTimeout = Timeout.InfiniteTimeSpan// TimeSpan.FromMinutes(10),
                        //KeepAliveInterval = TimeSpan.FromMinutes(1)
                    };
                    return ValueTask.FromResult(options);
                },
                ListenEndPoint = new IPEndPoint(IPAddress.Any, 52301)
            });
            _serverTask = QuicRpcServerConnectionHandle(_listener, _cts.Token);
        }

        private async Task QuicRpcServerConnectionHandle(QuicListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var connection = await listener.AcceptConnectionAsync(cancellationToken);
                    _ = _quicRpcService.HandleConnection(connection, new BenchmarkRpcContext(), throwOnClose: false, cancellationToken: cancellationToken);
                }
                catch
                {
                    return;
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _cts!.CancelAsync();
            await _serverTask!;
            await _listener!.DisposeAsync(); ;
        }
    }
}
