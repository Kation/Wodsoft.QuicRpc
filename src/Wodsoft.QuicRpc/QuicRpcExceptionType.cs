using System;
using System.Collections.Generic;
using System.Text;

namespace Wodsoft.QuicRpc
{
    public enum QuicRpcExceptionType : long
    {
        RemoteException = 1,
        FunctionNotFound = 2,
        RemoteShutdown = 3,
        SignatureError = 4,
        ProtocolError = 5
    }
}
