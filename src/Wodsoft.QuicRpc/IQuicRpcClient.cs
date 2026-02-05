using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Quic;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    /// <summary>
    /// 由源生成器生成的客户端结构应实现此接口以便在运行时绑定到服务与连接。
    /// </summary>
    public interface IQuicRpcClient
    {
        /// <summary>
        /// 将客户端绑定到指定的QuicRpc服务和QUIC连接。
        /// 由源生成器生成的客户端实现会在此方法中保存服务实例和连接实例以便后续调用。
        /// </summary>
        void Bind(QuicRpcService quicRpcService, QuicConnection connection);
    }

}
