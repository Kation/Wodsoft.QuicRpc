using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    public abstract class QuicRpcSerializer
    {
        public abstract ValueTask<T?> DeserializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(Stream stream, CancellationToken cancellationToken = default);

        public abstract ValueTask SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default);

        public static readonly QuicRpcSerializer Default = new QuicRpcMemoryPackSerializer();
    }
}
