using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Quic;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    /// <summary>
    /// 表示QuicRpc调用的上下文信息，包含连接上下文、取消令牌以及可能的流对象。
    /// </summary>
    public struct QuicRpcContext<T>
    {
        /// <summary>
        /// 实例化一个新的QuicRpcContext结构，包含连接上下文、取消令牌以及可能的流对象。
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="connectionContext"></param>
        /// <param name="cancellationToken"></param>
        public QuicRpcContext(QuicStream? stream, T connectionContext, CancellationToken cancellationToken)
        {
            Stream = stream;
            ConnectionContext = connectionContext;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// 获取连接相关的用户自定义上下文对象。
        /// </summary>
        public T ConnectionContext { get; }

        /// <summary>
        /// 获取当前调用的取消令牌。
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// 获取当前调用使用的 QuicStream，非流式调用时为空。
        /// </summary>
        public QuicStream? Stream { get; }
    }
}
