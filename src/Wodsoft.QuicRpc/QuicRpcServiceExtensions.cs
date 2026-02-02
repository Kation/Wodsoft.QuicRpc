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
    public static class QuicRpcServiceExtensions
    {
        public static void BindFunctions<TContext, TFunctions>(this QuicRpcService<TContext> service, TFunctions functions)
            where TFunctions : class, IQuicRpcFunctions
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            if (functions == null)
                throw new ArgumentNullException(nameof(functions));
            functions.Bind(service);
        }

        public static void BindClient<TContext, TClient>(this QuicRpcService<TContext> service, QuicConnection connection, ref TClient client)
            where TClient: struct, IQuicRpcClient
        {
            client.Bind(service, connection);
        }
    }
}
