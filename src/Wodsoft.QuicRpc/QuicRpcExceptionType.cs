using System;
using System.Collections.Generic;
using System.Text;

namespace Wodsoft.QuicRpc
{
    /// <summary>
    /// QuicRpc异常类型。
    /// </summary>
    public enum QuicRpcExceptionType : long
    {
        /// <summary>
        /// 远端执行调用时发生异常。
        /// </summary>
        RemoteException = 1,
        /// <summary>
        /// 服务端未实现调用的服务方法。
        /// </summary>
        FunctionNotFound = 2,
        /// <summary>
        /// 远端已关闭/关闭中导致调用被中止。
        /// </summary>
        RemoteShutdown = 3,
        /// <summary>
        /// 本地和远端的服务方法签名不匹配。
        /// </summary>
        SignatureError = 4,
        /// <summary>
        /// 解析协议时发现无法识别的数据。
        /// </summary>
        ProtocolError = 5
    }
}
