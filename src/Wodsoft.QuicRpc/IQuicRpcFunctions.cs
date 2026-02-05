using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    /// <summary>
    /// 表示QuicRpc服务。
    /// </summary>
    public interface IQuicRpcFunctions
    {
        /// <summary>
        /// 将方法注册到指定的QuicRpcService实例。此方法通常由源生成器生成。
        /// </summary>
        /// <typeparam name="TContext">服务的上下文类型。</typeparam>
        /// <param name="service">要绑定的QuicRpcService实例。</param>
        void Bind<TContext>(QuicRpcService<TContext> service);
    }
}
