using MemoryPack;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Quic;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Wodsoft.QuicRpc.Benchmarks")]
[assembly: InternalsVisibleTo("Wodsoft.QuicRpc.ConsoleTest")]
[assembly: InternalsVisibleTo("Wodsoft.Mole.UnitTest")]

#pragma warning disable CA1416 // 验证平台兼容性
namespace Wodsoft.QuicRpc
{
    /// <summary>
    /// QuicRpc服务的抽象基类。
    /// </summary>
    public abstract class QuicRpcService
    {
        /// <summary>
        /// 异步调用带有请求和响应类型的 Function。
        /// </summary>
        /// <typeparam name="TRequest">请求类型。</typeparam>
        /// <typeparam name="TResponse">响应类型。</typeparam>
        /// <param name="stream">用于通信的QuicStream。</param>
        /// <param name="functionId">要调用的服务方法的标识符。</param>
        /// <param name="request">要发送的请求对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>返回包含响应的异步任务。</returns>
        public abstract ValueTask<TResponse> InvokeFunctionAsync<TRequest, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse>(QuicStream stream, ushort functionId, TRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步调用无请求但有响应的 Function。
        /// </summary>
        /// <typeparam name="TResponse">响应类型。</typeparam>
        /// <param name="stream">用于通信的QuicStream。</param>
        /// <param name="functionId">要调用的服务方法的标识符。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>返回包含响应的异步任务。</returns>
        public abstract ValueTask<TResponse> InvokeFunctionAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse>(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步调用带有请求但无响应的 Function。
        /// </summary>
        /// <typeparam name="TRequest">请求类型。</typeparam>
        /// <param name="stream">用于通信的QuicStream。</param>
        /// <param name="functionId">要调用的服务方法的标识符。</param>
        /// <param name="request">要发送的请求对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>返回表示操作完成的异步任务。</returns>
        public abstract ValueTask InvokeFunctionAsync<TRequest>(QuicStream stream, ushort functionId, TRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步调用无请求且无响应的 Function。
        /// </summary>
        /// <param name="stream">用于通信的QuicStream。</param>
        /// <param name="functionId">要调用的服务方法的标识符。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>返回表示操作完成的异步任务。</returns>
        public abstract ValueTask InvokeFunctionAsync(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步调用返回流式响应的 Function，成功时返回远端的 QuicStream 用于后续流式通信。
        /// </summary>
        /// <param name="stream">用于通信的QuicStream。</param>
        /// <param name="functionId">要调用的服务方法的标识符。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>返回包含远端QuicStream的异步任务。</returns>
        public abstract ValueTask<QuicStream> InvokeStreamingFunctionAsync(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// QuicRpc服务，包含注册服务方法和处理连接的逻辑。
    /// </summary>
    /// <typeparam name="TContext">连接相关的用户上下文类型。</typeparam>
    public class QuicRpcService<TContext> : QuicRpcService
    {
        private readonly MethodCall[] _functions;
        private readonly QuicRpcSerializer _serializer;
        private const byte _Placeholder = 0x78;

        /// <summary>
        /// 使用默认序列化器创建QuicRpcService实例。
        /// </summary>
        public QuicRpcService() : this(QuicRpcSerializer.Default)
        {

        }

        /// <summary>
        /// 使用指定的序列化器创建QuicRpcService实例。
        /// </summary>
        /// <param name="serializer">用于序列化/反序列化的QuicRpcSerializer实例。</param>
        public QuicRpcService(QuicRpcSerializer serializer)
        {
            _functions = new MethodCall[65536];
            _serializer = serializer;
        }

        /// <summary>
        /// 注册带请求参数和响应参数的服务方法实现。
        /// </summary>
        /// <typeparam name="TRequest">请求类型。</typeparam>
        /// <typeparam name="TResponse">响应类型。</typeparam>
        /// <param name="functionId">服务方法标识符。</param>
        /// <param name="func">处理函数。</param>
        public void RegisterFunction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse>(ushort functionId, Func<QuicRpcContext<TContext>, TRequest, ValueTask<TResponse>> func)
        {
            EnsureFunctionNotRegistered(functionId);
            MethodCall invokeFunc = async (stream, context, header, cancellationToken) =>
            {
                var request = (await _serializer.DeserializeAsync<TRequest>(stream, cancellationToken).ConfigureAwait(false))!;
                var response = await func.Invoke(new QuicRpcContext<TContext>(null, context, cancellationToken), request).ConfigureAwait(false);
                if (response == null)
                {
                    stream.Abort(QuicAbortDirection.Both, (long)QuicRpcExceptionType.RemoteException);
                    throw new InvalidOperationException("QuicRpc response can't be null.");
                }
                header.Span[1] = (byte)QuicRpcResult.Response;
                await stream.WriteAsync(header.Slice(0, 2), cancellationToken).ConfigureAwait(false);
                await _serializer.SerializeAsync(stream, response, cancellationToken).ConfigureAwait(false);
                stream.CompleteWrites();
            };
            RegisterFunction(functionId, invokeFunc);
        }

        /// <summary>
        /// 注册无请求参数但有响应参数的服务方法实现。
        /// </summary>
        /// <typeparam name="TResponse">响应类型。</typeparam>
        /// <param name="functionId">服务方法标识符。</param>
        /// <param name="func">处理函数。</param>
        public void RegisterFunction<TResponse>(ushort functionId, Func<QuicRpcContext<TContext>, ValueTask<TResponse>> func)
        {
            EnsureFunctionNotRegistered(functionId);
            MethodCall invokeFunc = async (stream, context, header, cancellationToken) =>
            {
                var response = await func.Invoke(new QuicRpcContext<TContext>(stream, context, cancellationToken)).ConfigureAwait(false);
                if (response == null)
                {
                    stream.Abort(QuicAbortDirection.Both, (long)QuicRpcExceptionType.RemoteException);
                    throw new InvalidOperationException("QuicRpc response can't be null.");
                }
                header.Span[1] = (byte)QuicRpcResult.Response;
                await stream.WriteAsync(header.Slice(0, 2), cancellationToken).ConfigureAwait(false);
                await _serializer.SerializeAsync(stream, response, cancellationToken).ConfigureAwait(false);
                stream.CompleteWrites();
            };
            RegisterFunction(functionId, invokeFunc);
        }

        /// <summary>
        /// 注册带请求参数但无响应参数的服务方法实现。
        /// </summary>
        /// <typeparam name="TRequest">请求类型。</typeparam>
        /// <param name="functionId">服务方法标识符。</param>
        /// <param name="func">处理函数。</param>
        public void RegisterFunction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TRequest>(ushort functionId, Func<QuicRpcContext<TContext>, TRequest, ValueTask> func)
        {
            EnsureFunctionNotRegistered(functionId);
            MethodCall invokeFunc = async (stream, context, header, cancellationToken) =>
            {
                var request = (await _serializer.DeserializeAsync<TRequest>(stream, cancellationToken).ConfigureAwait(false))!;
                await func.Invoke(new QuicRpcContext<TContext>(null, context, cancellationToken), request).ConfigureAwait(false);
                header.Span[1] = (byte)QuicRpcResult.Success;
                await stream.WriteAsync(header.Slice(0, 2), true, cancellationToken).ConfigureAwait(false);
            };
            RegisterFunction(functionId, invokeFunc);
        }

        /// <summary>
        /// 注册无请求参数且无响应参数的服务方法实现。
        /// </summary>
        /// <param name="functionId">服务方法标识符。</param>
        /// <param name="func">处理函数。</param>
        public void RegisterFunction(ushort functionId, Func<QuicRpcContext<TContext>, ValueTask> func)
        {
            EnsureFunctionNotRegistered(functionId);
            MethodCall invokeFunc = async (stream, context, header, cancellationToken) =>
            {
                await func.Invoke(new QuicRpcContext<TContext>(null, context, cancellationToken)).ConfigureAwait(false);
                header.Span[1] = (byte)QuicRpcResult.Success;
                await stream.WriteAsync(header.Slice(0, 2), true, cancellationToken).ConfigureAwait(false);
            };
            RegisterFunction(functionId, invokeFunc);
        }

        /// <summary>
        /// 注册流式服务方法，服务方法将通过上下文里的QuicStream进行数据处理。
        /// </summary>
        /// <param name="functionId">服务方法标识符。</param>
        /// <param name="func">处理函数。</param>
        public void RegisterStreamingFunction(ushort functionId, Func<QuicRpcContext<TContext>, ValueTask> func)
        {
            EnsureFunctionNotRegistered(functionId);
            MethodCall invokeFunc = async (stream, context, header, cancellationToken) =>
            {
                header.Span[1] = (byte)QuicRpcResult.Streaming;
                await stream.WriteAsync(header.Slice(0, 2), cancellationToken).ConfigureAwait(false);
                await func.Invoke(new QuicRpcContext<TContext>(stream, context, cancellationToken)).ConfigureAwait(false);
            };
            RegisterFunction(functionId, invokeFunc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureFunctionNotRegistered(ushort functionId)
        {
            if (_functions[functionId] == null)
                return;
            throw new InvalidOperationException("服务方法already registered.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RegisterFunction(ushort functionId, MethodCall function)
        {
            _functions[functionId] = function;
        }

        /// <inheritdoc/>
        public override async ValueTask<TResponse> InvokeFunctionAsync<TRequest, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse>(QuicStream stream, ushort functionId, TRequest request, CancellationToken cancellationToken = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(2);
            try
            {
                var header = buffer.AsMemory().Slice(0, 2);
                MemoryMarshal.Write(header.Span, functionId);
                await stream.WriteAsync(header).ConfigureAwait(false);
                await _serializer.SerializeAsync(stream, request, cancellationToken).ConfigureAwait(false);
                stream.CompleteWrites();
                var read = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc not configure successfully at remote.");
                if (header.Span[0] != _Placeholder)
                    throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                if (read != 2)
                {
                    read = await stream.ReadAsync(header.Slice(read, 2 - read), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                }
                var result = (QuicRpcResult)header.Span[1];
                switch (result)
                {
                    case QuicRpcResult.Streaming:
                    case QuicRpcResult.Success:
                        throw new QuicRpcException(QuicRpcExceptionType.SignatureError, "QuicRpc服务方法signature not equal to remote.");
                    case QuicRpcResult.Response:
                        break;
                    default:
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                }
                return (await _serializer.DeserializeAsync<TResponse>(stream, cancellationToken).ConfigureAwait(false))!;
            }
            catch (QuicException ex)
            {
                if (ex.ApplicationErrorCode != null)
                {
                    switch ((QuicRpcExceptionType)ex.ApplicationErrorCode.Value)
                    {
                        case QuicRpcExceptionType.RemoteException:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteException, "QuicRpc invoke with exception at remote.");
                        case QuicRpcExceptionType.FunctionNotFound:
                            throw new QuicRpcException(QuicRpcExceptionType.FunctionNotFound, "QuicRpc服务方法not found at remote.");
                        case QuicRpcExceptionType.RemoteShutdown:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteShutdown, "QuicRpc server shutting down.");
                    }
                }
                throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <inheritdoc/>
        public override async ValueTask<TResponse> InvokeFunctionAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse>(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(2);
            try
            {
                var header = buffer.AsMemory().Slice(0, 2);
                MemoryMarshal.Write(header.Span, functionId);
                    await stream.WriteAsync(header, true).ConfigureAwait(false);
                var read = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc not configure successfully at remote.");
                if (header.Span[0] != _Placeholder)
                    throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                if (read != 2)
                {
                    read = await stream.ReadAsync(header.Slice(read, 2 - read), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                }
                var result = (QuicRpcResult)header.Span[1];
                switch (result)
                {
                    case QuicRpcResult.Streaming:
                    case QuicRpcResult.Success:
                        throw new QuicRpcException(QuicRpcExceptionType.SignatureError, "QuicRpc服务方法signature not equal to remote.");
                    case QuicRpcResult.Response:
                        break;
                    default:
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                }
                return (await _serializer.DeserializeAsync<TResponse>(stream, cancellationToken).ConfigureAwait(false))!;
            }
            catch (QuicException ex)
            {
                if (ex.ApplicationErrorCode != null)
                {
                    switch ((QuicRpcExceptionType)ex.ApplicationErrorCode.Value)
                    {
                        case QuicRpcExceptionType.RemoteException:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteException, "QuicRpc invoke with exception at remote.");
                        case QuicRpcExceptionType.FunctionNotFound:
                            throw new QuicRpcException(QuicRpcExceptionType.FunctionNotFound, "QuicRpc服务方法not found at remote.");
                        case QuicRpcExceptionType.RemoteShutdown:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteShutdown, "QuicRpc server shutting down.");
                    }
                }
                throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <inheritdoc/>
        public override async ValueTask InvokeFunctionAsync<TRequest>(QuicStream stream, ushort functionId, TRequest request, CancellationToken cancellationToken = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(2);
            try
            {
                var header = buffer.AsMemory().Slice(0, 2);
                MemoryMarshal.Write(header.Span, functionId);
                await stream.WriteAsync(header).ConfigureAwait(false);
                await _serializer.SerializeAsync(stream, request, cancellationToken).ConfigureAwait(false);
                stream.CompleteWrites();
                var read = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc not configure successfully at remote.");
                if (header.Span[0] != _Placeholder)
                    throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                if (read != 2)
                {
                    read = await stream.ReadAsync(header.Slice(read, 2 - read), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                }
                var result = (QuicRpcResult)header.Span[1];
                switch (result)
                {
                    case QuicRpcResult.Streaming:
                    case QuicRpcResult.Response:
                        throw new QuicRpcException(QuicRpcExceptionType.SignatureError, "QuicRpc服务方法signature not equal to remote.");
                    case QuicRpcResult.Success:
                        break;
                    default:
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                }
            }
            catch (QuicException ex)
            {
                if (ex.ApplicationErrorCode != null)
                {
                    switch ((QuicRpcExceptionType)ex.ApplicationErrorCode.Value)
                    {
                        case QuicRpcExceptionType.RemoteException:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteException, "QuicRpc invoke with exception at remote.");
                        case QuicRpcExceptionType.FunctionNotFound:
                            throw new QuicRpcException(QuicRpcExceptionType.FunctionNotFound, "QuicRpc服务方法not found at remote.");
                        case QuicRpcExceptionType.RemoteShutdown:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteShutdown, "QuicRpc server shutting down.");
                    }
                }
                throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <inheritdoc/>
        public override async ValueTask InvokeFunctionAsync(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(2);
            try
            {
                var header = buffer.AsMemory().Slice(0, 2);
                MemoryMarshal.Write(header.Span, functionId);
                await stream.WriteAsync(header, true).ConfigureAwait(false);
                Debug.WriteLine("Header sent.");
                var read = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc not configure successfully at remote.");
                if (header.Span[0] != _Placeholder)
                    throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                if (read != 2)
                {
                    read = await stream.ReadAsync(header.Slice(read, 2 - read), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                }
                var result = (QuicRpcResult)header.Span[1];
                switch (result)
                {
                    case QuicRpcResult.Streaming:
                    case QuicRpcResult.Response:
                        throw new QuicRpcException(QuicRpcExceptionType.SignatureError, "QuicRpc服务方法signature not equal to remote.");
                    case QuicRpcResult.Success:
                        break;
                    default:
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                }
            }
            catch (QuicException ex)
            {
                if (ex.ApplicationErrorCode != null)
                {
                    switch ((QuicRpcExceptionType)ex.ApplicationErrorCode.Value)
                    {
                        case QuicRpcExceptionType.RemoteException:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteException, "QuicRpc invoke with exception at remote.");
                        case QuicRpcExceptionType.FunctionNotFound:
                            throw new QuicRpcException(QuicRpcExceptionType.FunctionNotFound, "QuicRpc服务方法not found at remote.");
                        case QuicRpcExceptionType.RemoteShutdown:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteShutdown, "QuicRpc server shutting down.");
                    }
                }
                throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, ex, "QuicRpc protocol error.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <inheritdoc/>
        public override async ValueTask<QuicStream> InvokeStreamingFunctionAsync(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(2);
            try
            {
                var header = buffer.AsMemory().Slice(0, 2);
                MemoryMarshal.Write(header.Span, functionId);
                await stream.WriteAsync(header).ConfigureAwait(false);
                var read = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc not configure successfully at remote.");
                if (header.Span[0] != _Placeholder)
                    throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                if (read != 2)
                {
                    read = await stream.ReadAsync(header.Slice(read, 2 - read), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                }
                var result = (QuicRpcResult)header.Span[1];
                switch (result)
                {
                    case QuicRpcResult.Success:
                    case QuicRpcResult.Response:
                        throw new QuicRpcException(QuicRpcExceptionType.SignatureError, "QuicRpc服务方法signature not equal to remote.");
                    case QuicRpcResult.Streaming:
                        break;
                    default:
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                }
                return stream;
            }
            catch (QuicException ex)
            {
                if (ex.ApplicationErrorCode != null)
                {
                    switch ((QuicRpcExceptionType)ex.ApplicationErrorCode.Value)
                    {
                        case QuicRpcExceptionType.RemoteException:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteException, "QuicRpc invoke with exception at remote.");
                        case QuicRpcExceptionType.FunctionNotFound:
                            throw new QuicRpcException(QuicRpcExceptionType.FunctionNotFound, "QuicRpc服务方法not found at remote.");
                        case QuicRpcExceptionType.RemoteShutdown:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteShutdown, "QuicRpc server shutting down.");
                    }
                }
                throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// 处理Quic连接。<br/>
        /// 内部会循环接受传入流并调用HandleStream进行处理。
        /// </summary>
        /// <param name="connection">Quic连接。</param>
        /// <param name="context">用户连接上下文。</param>
        /// <param name="throwOnCancel">取消时引发异常。</param>
        /// <param name="throwOnClose">关闭时引发异常。</param>
        /// <param name="exceptionDelegate">处理异常的过程出现异常时回调委托。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>返回处理任务。</returns>
        public async Task HandleConnection(QuicConnection connection, TContext context, bool throwOnCancel = false, bool throwOnClose = false, Action<Exception>? exceptionDelegate = null, CancellationToken cancellationToken = default)
        {
            int count = 0;
            TaskCompletionSource? tcs = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    //接受传入流
                    var stream = await connection.AcceptInboundStreamAsync(cancellationToken).ConfigureAwait(false);
                    Debug.Assert(stream != null);
                    Interlocked.Increment(ref count);
                    var task = HandleStream(stream, context, exceptionDelegate, cancellationToken).ContinueWith(_ =>
                    {
                        var value = Interlocked.Decrement(ref count);
                        if (value == 0)
                            Volatile.Read(ref tcs)?.SetResult();
                    });
                }
                catch (QuicException ex)
                {
                    if (throwOnClose)
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                    break;
                }
                catch (OperationCanceledException ex)
                {
                    if (throwOnCancel)
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
                    break;
                }
            }
            //等待任务完成
            if (Volatile.Read(ref count) == 0)
                return;
            var t = new TaskCompletionSource();
            Volatile.Write(ref tcs, t);
            if (Volatile.Read(ref count) == 0)
                return;
            await t.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// 处理Quic流。
        /// </summary>
        /// <param name="stream">Quic流。</param>
        /// <param name="context">用户连接上下文。</param>
        /// <param name="exceptionDelegate">处理异常的过程出现异常时回调委托。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>返回处理任务。</returns>
        public async Task HandleStream(QuicStream stream, TContext context, Action<Exception>? exceptionDelegate, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(2);
            try
            {
                var header = buffer.AsMemory().Slice(0, 2);
                try
                {
                    await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
                    MethodCall func = _functions[MemoryMarshal.Read<ushort>(header.Span)];
                    if (func == null)
                    {
                        stream.Abort(QuicAbortDirection.Both, (long)QuicRpcExceptionType.FunctionNotFound);
                        return;
                    }
                    header.Span[0] = _Placeholder;
                    await func(stream, context, header, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    stream.Abort(QuicAbortDirection.Both, (long)QuicRpcExceptionType.RemoteShutdown);
                    return;
                }
                catch (OperationCanceledException)
                {
                    stream.Abort(QuicAbortDirection.Both, (long)QuicRpcExceptionType.RemoteShutdown);
                    return;
                }
                catch (QuicException)
                {
                    return;
                }
                catch (EndOfStreamException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    stream.Abort(QuicAbortDirection.Both, (long)QuicRpcExceptionType.RemoteException);
                    exceptionDelegate?.Invoke(ex);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }

        private delegate Task MethodCall(QuicStream stream, TContext context, Memory<byte> header, CancellationToken cancellationToken);
    }
}