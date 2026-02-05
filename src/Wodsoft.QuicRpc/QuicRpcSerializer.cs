using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    /// <summary>
    /// 用于QuicRpc序列化与反序列化的抽象基类。
    /// </summary>
    public abstract class QuicRpcSerializer
    {
        /// <summary>
        /// 从流中异步反序列化对象。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="stream">输入流。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步返回反序列化的对象或null。</returns>
        public abstract ValueTask<T?> DeserializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(Stream stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// 将对象异步序列化到流中。
        /// </summary>
        /// <typeparam name="T">要序列化的类型。</typeparam>
        /// <param name="stream">目标流。</param>
        /// <param name="value">要序列化的对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示序列化操作的任务。</returns>
        public abstract ValueTask SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default);

        /// <summary>
        /// 默认的序列化器实现（基于MemoryPack）。
        /// </summary>
        public static readonly QuicRpcSerializer Default = new QuicRpcMemoryPackSerializer();
    }
}
