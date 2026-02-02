using BenchmarkDotNet.Attributes;
using MemoryPack;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1416 // 验证平台兼容性
namespace Wodsoft.QuicRpc.Benchmarks
{
    public class SerializationBenchmark
    {
        private CancellationTokenSource _cts;
        private QuicListener _listener;
        private QuicConnection _clientConnection, _serverConnection;


        [GlobalSetup]
        public async Task GlobalSetup()
        {
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
            var acceptTask = _listener.AcceptConnectionAsync();
            _clientConnection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
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
            _serverConnection = await acceptTask;
            _cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    await _listener.AcceptConnectionAsync(_cts.Token);
                }
            });
        }

        [GlobalCleanup]
        public async Task GLobalCleanup()
        {
            await _cts.CancelAsync();
            await _clientConnection.DisposeAsync();
            await _serverConnection.DisposeAsync();
            await _listener.DisposeAsync();
        }

        public IEnumerable<object[]> Parameters()
        {
            //yield return new object[] { 100, 1 };
            //yield return new object[] { 100, 8 };
            yield return new object[] { 1000, 8 };
            yield return new object[] { 5000, 8 };
            yield return new object[] { 5000, 16 };
            yield return new object[] { 5000, 32 };
            yield return new object[] { 5000, 64 };
            yield return new object[] { 5000, 128 };
        }

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(Parameters))]
        public async Task MemoryPack(int Batch, int Thread)
        {
            var hello = new Hello2 { Name = "Benchmark" };
            await Parallel.ForAsync(0, Batch, new ParallelOptions { MaxDegreeOfParallelism = Thread }, async (i, c) =>
            {
                var clientStream = await _clientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
                await MemoryPackSerializer.SerializeAsync(clientStream, hello);
                clientStream.CompleteWrites();
                var serverStream = await _serverConnection.AcceptInboundStreamAsync();
                await MemoryPackSerializer.DeserializeAsync<Hello2>(serverStream);
                await clientStream.DisposeAsync();
                await serverStream.DisposeAsync();
            });
        }

        [Benchmark]
        [ArgumentsSource(nameof(Parameters))]
        public async Task Protobuf(int Batch, int Thread)
        {
            var hello = new Hello { Name = "Benchmark" };
            await Parallel.ForAsync(0, Batch, new ParallelOptions { MaxDegreeOfParallelism = Thread }, async (i, c) =>
            {
                var clientStream = await _clientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
                var codeOutputStream = new Google.Protobuf.CodedOutputStream(clientStream, true);
                hello.WriteTo(codeOutputStream);
                codeOutputStream.Flush();
                clientStream.CompleteWrites();
                var serverStream = await _serverConnection.AcceptInboundStreamAsync();
                var codeInputStream = new Google.Protobuf.CodedInputStream(serverStream, true);
                var obj = new Hello();
                obj.MergeFrom(codeInputStream);
                await clientStream.DisposeAsync();
                await serverStream.DisposeAsync();
            });
        }
    }
}
