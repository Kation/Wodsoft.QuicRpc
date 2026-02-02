using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc.Benchmarks
{
    public class BenchmarkGrpcService : BenchmarkGrpc.BenchmarkGrpcBase
    {
        public override Task<Hello> Test(Hello request, ServerCallContext context)
        {
            return Task.FromResult(request);
        }
    }
}
