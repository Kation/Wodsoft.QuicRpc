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
        [QuicRpcFunction(3)]
        public partial Task Empty(CancellationToken cancellationToken = default);
    }
}
