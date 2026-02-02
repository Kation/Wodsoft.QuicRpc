using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    public abstract class QuicRpcFunctions
    {
        private class QuicRpcFunctionsContext<TContext>
        {
            public static readonly AsyncLocal<QuicRpcContext<TContext>> ContextLocal = new AsyncLocal<QuicRpcContext<TContext>>();
        }


        protected static void SetContext<TContext>(QuicRpcContext<TContext> context)
        {
            QuicRpcFunctionsContext<TContext>.ContextLocal.Value = context;
        }

        protected QuicRpcContext<TContext> GetContext<TContext>() => QuicRpcFunctionsContext<TContext>.ContextLocal.Value;
    }
}
