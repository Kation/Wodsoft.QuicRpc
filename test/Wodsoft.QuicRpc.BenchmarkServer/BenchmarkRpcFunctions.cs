using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc.BenchmarkServer
{
    [QuicRpcFunction(0)]
    public partial class BenchmarkRpcFunctions : QuicRpcFunctions
    {
        [QuicRpcFunction(0)]
        public ValueTask<string> BothWithParameter(string request)
        {
            return ValueTask.FromResult(request);
        }
    }
}
