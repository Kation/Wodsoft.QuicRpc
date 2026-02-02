using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Quic;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    public struct QuicRpcContext<T>
    {
        public QuicRpcContext(QuicStream? stream, T connectionContext, CancellationToken cancellationToken)
        {
            Stream = stream;
            ConnectionContext = connectionContext;
            CancellationToken = cancellationToken;
        }

        public T ConnectionContext { get; }

        public CancellationToken CancellationToken { get; }

        public QuicStream? Stream { get; }
    }
}
