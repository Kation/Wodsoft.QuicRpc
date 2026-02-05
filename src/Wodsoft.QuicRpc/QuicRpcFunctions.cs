using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    /// <summary>
    /// 为实现QuicRpc服务端的调用服务提供基础功能。<br/>
    /// 继承此类的用户类可以定义实际的调用方法。
    /// </summary>
    public abstract class QuicRpcFunctions
    {
        private class QuicRpcFunctionsContext<TContext>
        {
            public static readonly AsyncLocal<QuicRpcContext<TContext>> ContextLocal = new AsyncLocal<QuicRpcContext<TContext>>();
        }

        /// <summary>
        /// 设置当前调用上下文。
        /// </summary>
        /// <typeparam name="TContext">连接上下文类型。</typeparam>
        /// <param name="context">要设置的QuicRpcContext。</param>
        protected static void SetContext<TContext>(QuicRpcContext<TContext> context)
        {
            QuicRpcFunctionsContext<TContext>.ContextLocal.Value = context;
        }

        /// <summary>
        /// 获取当前调用的QuicRpc上下文。
        /// </summary>
        /// <typeparam name="TContext">连接上下文类型。</typeparam>
        /// <returns>当前上下文对象。</returns>
        protected QuicRpcContext<TContext> GetContext<TContext>() => QuicRpcFunctionsContext<TContext>.ContextLocal.Value;
    }
}
