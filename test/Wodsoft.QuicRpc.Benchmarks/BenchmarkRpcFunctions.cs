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
        [QuicRpcFunction(3)]
        public ValueTask Empty()
        {
            return ValueTask.CompletedTask;
        }
    }
}
