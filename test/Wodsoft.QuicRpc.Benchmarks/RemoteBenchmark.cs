using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CA1416 // 验证平台兼容性
namespace Wodsoft.QuicRpc.Benchmarks
{
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    public class RemoteBenchmark
    {
        private QuicRpcService<BenchmarkRpcContext> _quicRpcService;
        private BenchmarkRpcClient _quicRpcClient;
        private QuicConnection _quicRpcClientConnection;
        private CancellationTokenSource _cts;
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

        [GlobalSetup]
        public async Task GlobalSetup()
        {
            _cts = new CancellationTokenSource();

            _quicRpcService = new QuicRpcService<BenchmarkRpcContext>();
            _quicRpcClientConnection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
            {
                RemoteEndPoint = new IPEndPoint(new IPAddress([10, 128, 0, 66]), 52301),
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (_, _, _, _) =>
                    {
                        return true;
                    },
                    ApplicationProtocols = [SslApplicationProtocol.Http3],
                    TargetHost = "localhost",
                    EnabledSslProtocols = SslProtocols.Tls13
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
            await Parallel.ForAsync(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (_, _) => await _quicRpcClient.Empty());

            SocketsHttpHandler socketsHttp2Handler = new SocketsHttpHandler();
            socketsHttp2Handler.EnableMultipleHttp2Connections = true;
            socketsHttp2Handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };
            var http2Client = new HttpClient(socketsHttp2Handler);
            http2Client.DefaultRequestVersion = HttpVersion.Version20;
            http2Client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            _grpcChannelHttp2 = GrpcChannel.ForAddress($"https://10.128.0.66:52300", new GrpcChannelOptions { HttpClient = http2Client });
            _grpcClientHttp2 = new BenchmarkGrpc.BenchmarkGrpcClient(_grpcChannelHttp2);
            await Parallel.ForAsync(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (_, _) =>
                await _grpcClientHttp2.EmptyAsync(new Google.Protobuf.WellKnownTypes.Empty()));

            SocketsHttpHandler socketsHttp3Handler = new SocketsHttpHandler();
            socketsHttp3Handler.EnableMultipleHttp2Connections = true;
            socketsHttp3Handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };
            var http3Client = new HttpClient(socketsHttp3Handler);
            _grpcChannelHttp3 = GrpcChannel.ForAddress($"https://10.128.0.66:52300", new GrpcChannelOptions { HttpClient = http3Client, HttpVersion = HttpVersion.Version30, HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact });
            _grpcClientHttp3 = new BenchmarkGrpc.BenchmarkGrpcClient(_grpcChannelHttp3);
            await Parallel.ForAsync(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (_, _) =>
                await _grpcClientHttp3.EmptyAsync(new Google.Protobuf.WellKnownTypes.Empty()));
        }

        [GlobalCleanup]
        public async void GlobalCleanup()
        {
            _cts.Cancel();
            await _quicRpcClientConnection.DisposeAsync();
            _grpcChannelHttp2.Dispose();
            _grpcChannelHttp3.Dispose();
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
