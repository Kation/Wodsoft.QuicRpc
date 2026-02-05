using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Quic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    /// <summary>
    /// 为QuicRpcService提供的扩展方法，方便绑定函数集合与客户端。
    /// </summary>
    public static class QuicRpcServiceExtensions
    {
        /// <summary>
        /// 将实现了IQuicRpcFunctions的函数对象绑定到指定的服务实例。
        /// </summary>
        /// <typeparam name="TContext">服务上下文类型。</typeparam>
        /// <typeparam name="TFunctions">函数集合类型。</typeparam>
        /// <param name="service">要绑定到的服务实例。</param>
        /// <param name="functions">要绑定的函数集合。</param>
        public static void BindFunctions<TContext, TFunctions>(this QuicRpcService<TContext> service, TFunctions functions)
            where TFunctions : class, IQuicRpcFunctions
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            if (functions == null)
                throw new ArgumentNullException(nameof(functions));
            functions.Bind(service);
        }

        /// <summary>
        /// 将客户端绑定到服务和连接，使客户端可用于发起RPC调用。
        /// </summary>
        /// <typeparam name="TContext">服务上下文类型。</typeparam>
        /// <typeparam name="TClient">客户端结构类型，实现IQuicRpcClient。</typeparam>
        /// <param name="service">要绑定到的服务实例。</param>
        /// <param name="connection">要使用的QuicConnection。</param>
        /// <param name="client">要绑定的客户端实例。</param>
        public static void BindClient<TContext, TClient>(this QuicRpcService<TContext> service, QuicConnection connection, ref TClient client)
            where TClient: struct, IQuicRpcClient
        {
            client.Bind(service, connection);
        }
    }
}
