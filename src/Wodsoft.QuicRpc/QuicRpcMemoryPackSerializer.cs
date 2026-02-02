using MemoryPack;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Wodsoft.QuicRpc
{
    public class QuicRpcMemoryPackSerializer : QuicRpcSerializer
    {
        private readonly MemoryPackSerializerOptions _serializerOptions;

        public QuicRpcMemoryPackSerializer() : this(MemoryPackSerializerOptions.Default)
        {

        }

        public QuicRpcMemoryPackSerializer(MemoryPackSerializerOptions serializerOptions)
        {
            _serializerOptions = serializerOptions;
        }

        public override ValueTask<T?> DeserializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(Stream stream, CancellationToken cancellationToken = default) where T : default
        {
            return MemoryPackSerializer.DeserializeAsync<T>(stream, _serializerOptions, cancellationToken);
        }

        public override ValueTask SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
        {
            return MemoryPackSerializer.SerializeAsync(stream, value, _serializerOptions, cancellationToken);
        }
    }
}
