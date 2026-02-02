using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

#pragma warning disable CA1416 // 验证平台兼容性
namespace Wodsoft.QuicRpc.UnitTest
{
    public class GeneratorTests : QuicRpcTests, IClassFixture<CertificateContext>
    {
        public GeneratorTests(CertificateContext context) : base(context)
        {
        }

        [Fact]
        public async Task NoParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                var testFunctions = new TestRpcFunctions();
                serverService.BindFunctions(testFunctions);
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var testClient = new TestRpcClient();
                clientService.BindClient(connectionContext.ClientConnection, ref testClient);

                await testClient.Method1Async();

                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task RequestParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                var testFunctions = new TestRpcFunctions();
                serverService.BindFunctions(testFunctions);
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var testClient = new TestRpcClient();
                clientService.BindClient(connectionContext.ClientConnection, ref testClient);

                await testClient.Method2Async("Test");

                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task ResponseParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                var testFunctions = new TestRpcFunctions();
                serverService.BindFunctions(testFunctions);
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var testClient = new TestRpcClient();
                clientService.BindClient(connectionContext.ClientConnection, ref testClient);

                var result = await testClient.Method3Async();
                Assert.Equal("Ok", result);
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task BothWithParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                var testFunctions = new TestRpcFunctions();
                serverService.BindFunctions(testFunctions);
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var testClient = new TestRpcClient();
                clientService.BindClient(connectionContext.ClientConnection, ref testClient);

                var result = await testClient.Method4Async("Quic");
                Assert.Equal("Quic", result);

                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task MultipleThreadWithParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                var testFunctions = new TestRpcFunctions();
                serverService.BindFunctions(testFunctions);
                CancellationTokenSource cts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext(), cancellationToken: cts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var testClient = new TestRpcClient();
                clientService.BindClient(connectionContext.ClientConnection, ref testClient);

                await Parallel.ForAsync(0, 1000, async (i, c) =>
                {
                    var result = await testClient.Method4Async("Quic");
                    Assert.Equal("Quic", result);
                });
                await connectionContext.ClientConnection.DisposeAsync();
                await cts.CancelAsync();
                await serverTask;
            }
        }

        [Fact]
        public async Task TwoWayNoParameterAsync()
        {
            await using (var connectionContext = await GetConnectionContextAsync())
            {
                var serverService = new QuicRpcService<TestRpcContext>();
                var testFunctions = new TestRpcFunctions();
                serverService.BindFunctions(testFunctions);
                var serverClient = new TestRpcClient();
                serverService.BindClient(connectionContext.ServerConnection, ref serverClient);
                CancellationTokenSource serverCts = new CancellationTokenSource();
                var serverTask = serverService.HandleConnection(connectionContext.ServerConnection, new TestRpcContext { Value = "Server" }, cancellationToken: serverCts.Token);

                var clientService = new QuicRpcService<TestRpcContext>();
                var clientClient = new TestRpcClient();
                clientService.BindFunctions(testFunctions);
                clientService.BindClient(connectionContext.ClientConnection, ref clientClient);
                CancellationTokenSource clientCts = new CancellationTokenSource();
                var clientTask = clientService.HandleConnection(connectionContext.ClientConnection, new TestRpcContext { Value = "Client" }, cancellationToken: clientCts.Token);

                Assert.Equal("Server1", await clientClient.Method6Async("1"));
                Assert.Equal("Client2", await serverClient.Method6Async("2"));

                await clientCts.CancelAsync();
                await clientTask;
                await serverCts.CancelAsync();
                await serverTask;
            }
        }
    }
}
