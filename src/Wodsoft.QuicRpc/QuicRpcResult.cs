using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    internal enum QuicRpcResult : byte
    {
        Success = 0,
        FunctionNotFound = 1,
        Exception = 2,
        Streaming = 3,
        Response = 4
    }
}
