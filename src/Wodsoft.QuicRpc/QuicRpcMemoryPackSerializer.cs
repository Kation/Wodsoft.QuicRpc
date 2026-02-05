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
    /// <summary>
    /// 基于MemoryPack的QuicRpc序列化器实现。
    /// </summary>
    public class QuicRpcMemoryPackSerializer : QuicRpcSerializer
    {
        private readonly MemoryPackSerializerOptions _serializerOptions;

        /// <summary>
        /// 使用默认MemoryPack选项创建序列化器。
        /// </summary>
        public QuicRpcMemoryPackSerializer() : this(MemoryPackSerializerOptions.Default)
        {

        }

        /// <summary>
        /// 使用指定MemoryPack选项创建序列化器。
        /// </summary>
        /// <param name="serializerOptions">MemoryPack序列化选项。</param>
        public QuicRpcMemoryPackSerializer(MemoryPackSerializerOptions serializerOptions)
        {
            _serializerOptions = serializerOptions;
        }

        /// <inheritdoc/>
        public override ValueTask<T?> DeserializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(Stream stream, CancellationToken cancellationToken = default) where T : default
        {
            return MemoryPackSerializer.DeserializeAsync<T>(stream, _serializerOptions, cancellationToken);
        }

        /// <inheritdoc/>
        public override ValueTask SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
        {
            return MemoryPackSerializer.SerializeAsync(stream, value, _serializerOptions, cancellationToken);
        }
    }
}
