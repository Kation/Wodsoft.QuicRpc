using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc.Benchmarks
{
    [QuicRpcFunction(0)]
    public partial class BenchmarkRpcFunctions : QuicRpcFunctions
    {
        [QuicRpcFunction(0)]
        public ValueTask<string> Method1(string request)
        {
            return ValueTask.FromResult(request);
        }

        [QuicRpcFunction(1)]
        public ValueTask<Hello> Method2(Hello request)
        {
            return ValueTask.FromResult(request);
        }

        [QuicRpcFunction(2)]
        public ValueTask<Hello2> Method3(Hello2 request)
        {
            return ValueTask.FromResult(request);
        }
    }
}
