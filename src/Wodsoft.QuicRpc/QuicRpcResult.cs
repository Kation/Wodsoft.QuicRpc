using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    public enum QuicRpcResult : byte
    {
        Success = 0,
        FunctionNotFound = 1,
        Exception = 2,
        Streaming = 3,
        Response = 4,
        RemoteShutdown = 252,
        UnexpectedClosed = 253,
        SignatureError = 254,
        ProtocolError = 255
    }
}
