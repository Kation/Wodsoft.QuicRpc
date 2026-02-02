using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Wodsoft.QuicRpc;
using Xunit;

#pragma warning disable CA1416 // 验证平台兼容性
namespace Wodsoft.QuicRpc.UnitTest
{
    public class InvokeFunctionTests : QuicRpcTests, IClassFixture<CertificateContext>
    {
        public InvokeFunctionTests(CertificateContext context) : base(context)
        {

        }

        [Fact]
        public async Task BothWithParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                serverService.RegisterFunction<string, string>(1, (context, request) =>
                {
                    return ValueTask.FromResult($"Hi {request}.");
                });
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cts.Token);
                var result = await clientService.InvokeFunctionAsync<string, string>(clientStream, 1, "Quic");
                Assert.Equal("Hi Quic.", result);
                await clientStream.DisposeAsync();
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task TwiceBothWithParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                serverService.RegisterFunction<string, string>(1, (context, request) =>
                {
                    return ValueTask.FromResult($"Hi {request}.");
                });
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cts.Token);
                var result = await clientService.InvokeFunctionAsync<string, string>(clientStream, 1, "Quic");
                Assert.Equal("Hi Quic.", result);
                await clientStream.DisposeAsync();
                clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cts.Token);
                result = await clientService.InvokeFunctionAsync<string, string>(clientStream, 1, "Oh");
                Assert.Equal("Hi Oh.", result);

                await clientStream.DisposeAsync();
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task MultipleThreadBothWithParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                serverService.RegisterFunction<string, string>(1, (context, request) =>
                {
                    return ValueTask.FromResult($"Hi {request}.");
                });
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                await Parallel.ForAsync(0, 100, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (_, _) =>
                {
                    var stream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
                    var result = await clientService.InvokeFunctionAsync<string, string>(stream, 1, "Quic");
                    await stream.DisposeAsync();
                    Assert.Equal("Hi Quic.", result);
                });
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task WithRequestParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                serverService.RegisterFunction<string>(1, (context, request) =>
                {
                    return ValueTask.CompletedTask;
                });
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cts.Token);
                await clientService.InvokeFunctionAsync<string>(clientStream, 1, "Quic");

                await clientStream.DisposeAsync();
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task WithResponseParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                serverService.RegisterFunction<string>(1, (context) =>
                {
                    return ValueTask.FromResult($"Hi Quic.");
                });
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cts.Token);

                var result = await clientService.InvokeFunctionAsync<string>(clientStream, 1);
                Assert.Equal("Hi Quic.", result);

                await clientStream.DisposeAsync();
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task WithRequestParameter_CallBothWithParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                serverService.RegisterFunction<string, string>(1, (context, request) =>
                {
                    return ValueTask.FromResult($"Hi {request}.");
                });
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cts.Token);

                await Assert.ThrowsAsync<QuicRpcException>(() => clientService.InvokeFunctionAsync<string>(clientStream, 1, "Quic").AsTask());

                await clientStream.DisposeAsync();
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task NoParameterNoResponseAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                serverService.RegisterFunction(1, (context) =>
                {
                    return ValueTask.CompletedTask;
                });
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cts.Token);
                await clientService.InvokeFunctionAsync(clientStream, 1);

                await clientStream.DisposeAsync();
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task ResponseSignatureErrorAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                serverService.RegisterFunction<string>(1, (context) =>
                {
                    return ValueTask.FromResult("Success");
                });
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cts.Token);
                var ex = await Assert.ThrowsAsync<QuicRpcException>(async () => await clientService.InvokeFunctionAsync(clientStream, 1));
                Assert.Equal(QuicRpcExceptionType.SignatureError, ex.Type);
                await clientStream.DisposeAsync();
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task ShutdownAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                serverService.RegisterFunction(1, async (context) =>
                {
                    await Task.Delay(10000, context.CancellationToken);
                });
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
                var exTask = Assert.ThrowsAsync<QuicRpcException>(async () => await clientService.InvokeFunctionAsync(clientStream, 1))
                    .ContinueWith(task =>
                    {
                        Assert.Equal(QuicRpcExceptionType.RemoteShutdown, task.Result.Type);
                    });
                cts.CancelAfter(100);
                await exTask;
                await clientStream.DisposeAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task NotFoundAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional);
                await Assert.ThrowsAsync<QuicRpcException>(async () => await clientService.InvokeFunctionAsync(clientStream, 1))
                    .ContinueWith(task =>
                    {
                        Assert.Equal(QuicRpcExceptionType.FunctionNotFound, task.Result.Type);
                    });
                await clientStream.DisposeAsync();
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public void RegisterConflict()
        {
            var serverService = new QuicRpcService<TestRpcContext>();
            serverService.RegisterFunction(0, context => ValueTask.CompletedTask);
            Assert.Throws<InvalidOperationException>(() =>
            {
                serverService.RegisterFunction<string>(0, (context) => ValueTask.FromResult("123"));
            });
        }

        [Fact]
        public async Task StreamingAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                serverService.RegisterStreamingFunction(1, async context =>
                {
                    byte[] buffer = new byte[4];
                    while (!context.CancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await context.Stream!.ReadExactlyAsync(buffer.AsMemory(), context.CancellationToken);
                            MemoryMarshal.Write(buffer, MemoryMarshal.Read<int>(buffer) + 10);
                            await context.Stream.WriteAsync(buffer.AsMemory(), context.CancellationToken);
                        }
                        catch (QuicException)
                        {
                            return;
                        }
                    }
                });
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                await Parallel.ForAsync(0, 10, async (i, _) =>
                {
                    var clientStream = await connectionContext.ClientConnection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cts.Token);
                    var stream = await clientService.InvokeStreamingFunctionAsync(clientStream, 1);
                    byte[] clientBuffer = new byte[4];
                    for (int ii = 0; ii < 100; ii++)
                    {
                        MemoryMarshal.Write(clientBuffer, ii);
                        await stream.WriteAsync(clientBuffer.AsMemory(), cts.Token);
                        await stream.ReadExactlyAsync(clientBuffer.AsMemory(), cts.Token);
                        Assert.Equal(ii + 10, MemoryMarshal.Read<int>(clientBuffer));
                    }
                    await clientStream.DisposeAsync();
                });

                await cts.CancelAsync();
                await serverTask;
            }
        }
    }
}
