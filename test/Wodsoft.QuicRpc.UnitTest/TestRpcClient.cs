using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Quic;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc.UnitTest
{
    [QuicRpcFunction(0)]
    public partial struct TestRpcClient : IQuicRpcClient
    {
        [QuicRpcFunction(0)]
        public partial Task Method1Async();

        [QuicRpcFunction(1)]
        public partial Task Method2Async(string value);

        [QuicRpcFunction(2)]
        public partial Task<string> Method3Async();

        [QuicRpcFunction(3)]
        public partial Task<string> Method4Async(string value);

        [QuicRpcFunction(4, IsStreaming = true)]
        public partial Task<QuicStream> Method5Async();

        [QuicRpcFunction(5)]
        public partial Task<string> Method6Async(string value);
    }
}
