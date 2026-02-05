using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    /// <summary>
    /// 标记QuicRpc生成代码。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class QuicRpcFunctionAttribute : Attribute
    {
        /// <summary>
        /// 标记QuicRpc生成代码。
        /// </summary>
        /// <param name="id">方法或集合ID。</param>
        public QuicRpcFunctionAttribute(byte id)
        {
            Id = id;
        }

        /// <summary>
        /// 获取或设置方法或集合ID。
        /// </summary>
        public byte Id { get; }

        /// <summary>
        /// 获取或设置是否为流式方法。仅在标记方法时有效。
        /// </summary>
        public bool IsStreaming { get; set; }
    }
}
