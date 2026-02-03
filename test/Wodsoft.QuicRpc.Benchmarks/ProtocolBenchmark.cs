using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VSDiagnostics;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CA1416 // 验证平台兼容性
namespace Wodsoft.QuicRpc.Benchmarks
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    public class ProtocolBenchmark
    {
        private QuicRpcService<BenchmarkRpcContext> _quicRpcService;
        private BenchmarkRpcClient _quicRpcClient;
        private QuicConnection _quicRpcClientConnection;
        private CancellationTokenSource _cts;
        private QuicListener _listener;
        private Task _serverTask;
        private WebApplication _webApp;
        private GrpcChannel _grpcChannelHttp2, _grpcChannelHttp3;
        private BenchmarkGrpc.BenchmarkGrpcClient _grpcClientHttp2, _grpcClientHttp3;

        public IEnumerable<object[]> Parameters()
        {
            yield return new object[] { 1000, 1 };
            yield return new object[] { 1000, 8 };
            yield return new object[] { 5000, 8 };
            yield return new object[] { 5000, 16 };
            yield return new object[] { 5000, 32 };
        }

        [GlobalSetup(Target = "QuicRpc")]
        public async Task GlobalSetupQuicRpc()
        {
            _cts = new CancellationTokenSource();

            _quicRpcService = new QuicRpcService<BenchmarkRpcContext>();
            _quicRpcService.BindFunctions(new BenchmarkRpcFunctions());

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
                            EnabledSslProtocols = SslProtocols.Tls13,
                            ClientCertificateRequired = true,
                            RemoteCertificateValidationCallback = (_, _, _, _) => true
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
            _serverTask = QuicRpcServerConnectionHandle(_listener, _cts.Token);

            _quicRpcClientConnection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
            {
                RemoteEndPoint = _listener.LocalEndPoint,
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (_, _, _, _) =>
                    {
                        return true;
                    },
                    ApplicationProtocols = [SslApplicationProtocol.Http3],
                    TargetHost = "localhost",
                    EnabledSslProtocols = SslProtocols.Tls13,
                    ClientCertificates = [CertificateHelper.ClientCertificate]
                },
                DefaultCloseErrorCode = 0,
                DefaultStreamErrorCode = 0,
                MaxInboundBidirectionalStreams = 1024,
                MaxInboundUnidirectionalStreams = 128,
                IdleTimeout = TimeSpan.FromMinutes(10),
                KeepAliveInterval = TimeSpan.FromMinutes(1)
            });
            _quicRpcClient = new BenchmarkRpcClient();
            _quicRpcService.BindClient(_quicRpcClientConnection, ref _quicRpcClient);
            await Parallel.ForAsync(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (_, _) => await _quicRpcClient.Empty());
        }

        [GlobalSetup(Targets = ["Grpc", "GrpcWithQuic"])]
        public async Task GlobalSetupGrpc()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(52300, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                    listenOptions.UseHttps(CertificateHelper.ServerCertificate, adapterOptions =>
                    {
                        adapterOptions.SslProtocols = SslProtocols.Tls13;
                    });
                });
            });
            builder.Logging.SetMinimumLevel(LogLevel.Error);
            builder.Services.AddGrpc();
            _webApp = builder.Build();
            _webApp.MapGrpcService<BenchmarkGrpcService>();
            await _webApp.StartAsync();

            SocketsHttpHandler socketsHttp2Handler = new SocketsHttpHandler();
            socketsHttp2Handler.EnableMultipleHttp2Connections = true;
            socketsHttp2Handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };
            var http2Client = new HttpClient(socketsHttp2Handler);
            http2Client.DefaultRequestVersion = HttpVersion.Version20;
            http2Client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            _grpcChannelHttp2 = GrpcChannel.ForAddress($"https://localhost:52300", new GrpcChannelOptions { HttpClient = http2Client });
            _grpcClientHttp2 = new BenchmarkGrpc.BenchmarkGrpcClient(_grpcChannelHttp2);
            await Parallel.ForAsync(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (_, _) =>
                await _grpcClientHttp2.EmptyAsync(new Google.Protobuf.WellKnownTypes.Empty()));

            SocketsHttpHandler socketsHttp3Handler = new SocketsHttpHandler();
            socketsHttp3Handler.EnableMultipleHttp2Connections = true;
            socketsHttp3Handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };
            var http3Client = new HttpClient(socketsHttp3Handler);
            _grpcChannelHttp3 = GrpcChannel.ForAddress($"https://localhost:52300", new GrpcChannelOptions { HttpClient = http3Client, HttpVersion = HttpVersion.Version30, HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact });
            _grpcClientHttp3 = new BenchmarkGrpc.BenchmarkGrpcClient(_grpcChannelHttp3);
            await Parallel.ForAsync(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (_, _) =>
                await _grpcClientHttp3.EmptyAsync(new Google.Protobuf.WellKnownTypes.Empty()));
        }

        [GlobalCleanup(Targets = ["QuicRpc"])]
        public async void GlobalCleanupQuicRpc()
        {
            _cts.Cancel();
            await _quicRpcClientConnection.DisposeAsync();
            await _serverTask;
            await _listener.DisposeAsync();
        }

        [GlobalCleanup(Targets = ["Grpc", "GrpcWithQuic"])]
        public async void GlobalCleanupGrpc()
        {
            _grpcChannelHttp2.Dispose();
            _grpcChannelHttp3.Dispose();
            await _webApp.DisposeAsync();
        }

        private async Task QuicRpcServerConnectionHandle(QuicListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var connection = await listener.AcceptConnectionAsync(cancellationToken);
                    _serverTask = _quicRpcService.HandleConnection(connection, new BenchmarkRpcContext(), cancellationToken: cancellationToken);
                }
                catch
                {
                    return;
                }
            }
        }

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(Parameters))]
        public async Task QuicRpc(int Batch, int Thread)
        {
            if (Thread == 1)
            {
                for (int i = 0; i < Batch; i++)
                {
                    await _quicRpcClient.Empty().ConfigureAwait(false);
                }
            }
            else
            {
                await Parallel.ForAsync(0, Batch, new ParallelOptions { MaxDegreeOfParallelism = Thread }, async (_, _) =>
                    await _quicRpcClient.Empty().ConfigureAwait(false))
                .ConfigureAwait(false);
            }
        }

        [Benchmark]
        [ArgumentsSource(nameof(Parameters))]
        public async Task Grpc(int Batch, int Thread)
        {
            var empty = new Google.Protobuf.WellKnownTypes.Empty();
            if (Thread == 1)
            {
                for (int i = 0; i < Batch; i++)
                {
                    await _grpcClientHttp2.EmptyAsync(empty).ConfigureAwait(false);
                }
            }
            else
            {
                await Parallel.ForAsync(0, Batch, new ParallelOptions { MaxDegreeOfParallelism = Thread },
                    async (_, _) => await _grpcClientHttp2.EmptyAsync(empty).ConfigureAwait(false)).ConfigureAwait(false);
            }
        }

        [Benchmark]
        [ArgumentsSource(nameof(Parameters))]
        public async Task GrpcWithQuic(int Batch, int Thread)
        {
            var empty = new Google.Protobuf.WellKnownTypes.Empty();
            if (Thread == 1)
            {
                for (int i = 0; i < Batch; i++)
                {
                    await _grpcClientHttp3.EmptyAsync(empty).ConfigureAwait(false);
                }
            }
            else
            {
                await Parallel.ForAsync(0, Batch, new ParallelOptions { MaxDegreeOfParallelism = Thread },
                    async (_, _) => await _grpcClientHttp3.EmptyAsync(empty).ConfigureAwait(false)).ConfigureAwait(false);
            }
        }
    }
}
