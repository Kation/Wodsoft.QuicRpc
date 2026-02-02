using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc.UnitTest
{
    [QuicRpcFunction(0)]
    public partial class TestRpcFunctions : QuicRpcFunctions
    {
        [QuicRpcFunction(0)]
        public ValueTask Method1Async()
        {
            return ValueTask.CompletedTask;
        }

        [QuicRpcFunction(1)]
        public ValueTask Method2Async(string value)
        {
            return ValueTask.CompletedTask;
        }

        [QuicRpcFunction(2)]
        public ValueTask<string> Method3Async()
        {
            return ValueTask.FromResult("Ok");
        }

        [QuicRpcFunction(3)]
        public ValueTask<string> Method4Async(string value)
        {
            return ValueTask.FromResult(value);
        }

        [QuicRpcFunction(4, IsStreaming = true)]
        public ValueTask Method5Async()
        {
            return ValueTask.CompletedTask;
        }

        [QuicRpcFunction(5)]
        public ValueTask<string> Method6Async(string value)
        {
            var context = GetContext<TestRpcContext>();
            return ValueTask.FromResult(context.ConnectionContext.Value + value);
        }
    }
}
