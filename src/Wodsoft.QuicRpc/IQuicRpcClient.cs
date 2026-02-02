using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Quic;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{

    public interface IQuicRpcClient
    {
        void Bind(QuicRpcService quicRpcService, QuicConnection connection);
    }
}
