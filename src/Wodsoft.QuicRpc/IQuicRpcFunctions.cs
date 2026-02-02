using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    public interface IQuicRpcFunctions
    {
        void Bind<TContext>(QuicRpcService<TContext> service);
    }
}
