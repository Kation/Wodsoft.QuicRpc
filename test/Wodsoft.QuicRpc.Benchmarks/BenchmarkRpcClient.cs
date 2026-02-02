using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc.Benchmarks
{
    [QuicRpcFunction(0)]
    public partial struct BenchmarkRpcClient : IQuicRpcClient
    {
        [QuicRpcFunction(0)]
        public partial Task<string> Test(string request, CancellationToken cancellationToken = default);

        [QuicRpcFunction(1)]
        public partial Task<Hello> Test2(Hello request, CancellationToken cancellationToken = default);

        [QuicRpcFunction(2)]
        public partial Task<Hello2> Test3(Hello2 request, CancellationToken cancellationToken = default);
    }
}
