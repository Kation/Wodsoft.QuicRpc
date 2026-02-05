using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    /// <summary>
    /// QuicRpc调用异常，用于显示RPC调用过程遇到的错误类型信息。
    /// </summary>
    public class QuicRpcException : Exception
    {
        /// <summary>
        /// 实例化QuicRpcException。
        /// </summary>
        /// <param name="type">异常类型。</param>
        /// <param name="message">异常消息。</param>
        public QuicRpcException(QuicRpcExceptionType type, string message) : base(message)
        {
            Type = type;
        }

        /// <summary>
        /// 实例化QuicRpcException。
        /// </summary>
        /// <param name="type">异常类型。</param>
        /// <param name="ex">内部异常。</param>
        /// <param name="message">异常消息。</param>
        public QuicRpcException(QuicRpcExceptionType type, Exception ex, string message) : base(message)
        {
            Type = type;
        }

        /// <summary>
        /// 获取QuicRpc异常类型。
        /// </summary>
        public QuicRpcExceptionType Type { get; }
    }
}
