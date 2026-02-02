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
    public abstract class QuicRpcService
    {
        public abstract ValueTask<TResponse> InvokeFunctionAsync<TRequest, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse>(QuicStream stream, ushort functionId, TRequest request, CancellationToken cancellationToken = default);

        public abstract ValueTask<TResponse> InvokeFunctionAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse>(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default);

        public abstract ValueTask InvokeFunctionAsync<TRequest>(QuicStream stream, ushort functionId, TRequest request, CancellationToken cancellationToken = default);

        public abstract ValueTask InvokeFunctionAsync(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default);

        public abstract ValueTask<QuicStream> InvokeStreamingFunctionAsync(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default);
    }

    public class QuicRpcService<TContext> : QuicRpcService
    {
        private readonly MethodCall[] _functions;
        private readonly QuicRpcSerializer _serializer;
        private const byte _Placeholder = 0x78;

        public QuicRpcService() : this(QuicRpcSerializer.Default)
        {

        }

        public QuicRpcService(QuicRpcSerializer serializer)
        {
            _functions = new MethodCall[65536];
            _serializer = serializer;
        }

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
            throw new InvalidOperationException("Function already registered.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RegisterFunction(ushort functionId, MethodCall function)
        {
            _functions[functionId] = function;
        }

        public override async ValueTask<TResponse> InvokeFunctionAsync<TRequest, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse>(QuicStream stream, ushort functionId, TRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var header = MemoryPool<byte>.Shared.Rent(2))
                {
                    MemoryMarshal.Write(header.Memory.Span, functionId);
                    await stream.WriteAsync(header.Memory.Slice(0, 2)).ConfigureAwait(false);
                    await _serializer.SerializeAsync(stream, request, cancellationToken).ConfigureAwait(false);
                    stream.CompleteWrites();
                    var read = await stream.ReadAsync(header.Memory.Slice(0, 2), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc not configure successfully at remote.");
                    if (header.Memory.Span[0] != _Placeholder)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    if (read != 2)
                    {
                        read = await stream.ReadAsync(header.Memory.Slice(read, 2 - read), cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                            throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    }
                    var result = (QuicRpcResult)header.Memory.Span[1];
                    switch (result)
                    {
                        case QuicRpcResult.Streaming:
                        case QuicRpcResult.Success:
                            throw new QuicRpcException(QuicRpcExceptionType.SignatureError, "QuicRpc function signature not equal to remote.");
                        case QuicRpcResult.Response:
                            break;
                        default:
                            throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    }
                    return (await _serializer.DeserializeAsync<TResponse>(stream, cancellationToken).ConfigureAwait(false))!;
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
                            throw new QuicRpcException(QuicRpcExceptionType.FunctionNotFound, "QuicRpc function not found at remote.");
                        case QuicRpcExceptionType.RemoteShutdown:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteShutdown, "QuicRpc server shutting down.");
                    }
                }
                throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
            }
        }

        public override async ValueTask<TResponse> InvokeFunctionAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResponse>(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var header = MemoryPool<byte>.Shared.Rent(2))
                {
                    MemoryMarshal.Write(header.Memory.Span, functionId);
                    await stream.WriteAsync(header.Memory.Slice(0, 2), true).ConfigureAwait(false);
                    var read = await stream.ReadAsync(header.Memory.Slice(0, 2), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc not configure successfully at remote.");
                    if (header.Memory.Span[0] != _Placeholder)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    if (read != 2)
                    {
                        read = await stream.ReadAsync(header.Memory.Slice(read, 2 - read), cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                            throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    }
                    var result = (QuicRpcResult)header.Memory.Span[1];
                    switch (result)
                    {
                        case QuicRpcResult.Streaming:
                        case QuicRpcResult.Success:
                            throw new QuicRpcException(QuicRpcExceptionType.SignatureError, "QuicRpc function signature not equal to remote.");
                        case QuicRpcResult.Response:
                            break;
                        default:
                            throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    }
                    return (await _serializer.DeserializeAsync<TResponse>(stream, cancellationToken).ConfigureAwait(false))!;
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
                            throw new QuicRpcException(QuicRpcExceptionType.FunctionNotFound, "QuicRpc function not found at remote.");
                        case QuicRpcExceptionType.RemoteShutdown:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteShutdown, "QuicRpc server shutting down.");
                    }
                }
                throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
            }
        }

        public override async ValueTask InvokeFunctionAsync<TRequest>(QuicStream stream, ushort functionId, TRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var header = MemoryPool<byte>.Shared.Rent(2))
                {
                    MemoryMarshal.Write(header.Memory.Span, functionId);
                    await stream.WriteAsync(header.Memory.Slice(0, 2)).ConfigureAwait(false);
                    await _serializer.SerializeAsync(stream, request, cancellationToken).ConfigureAwait(false);
                    stream.CompleteWrites();
                    var read = await stream.ReadAsync(header.Memory.Slice(0, 2), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc not configure successfully at remote.");
                    if (header.Memory.Span[0] != _Placeholder)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    if (read != 2)
                    {
                        read = await stream.ReadAsync(header.Memory.Slice(read, 2 - read), cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                            throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    }
                    var result = (QuicRpcResult)header.Memory.Span[1];
                    switch (result)
                    {
                        case QuicRpcResult.Streaming:
                        case QuicRpcResult.Response:
                            throw new QuicRpcException(QuicRpcExceptionType.SignatureError, "QuicRpc function signature not equal to remote.");
                        case QuicRpcResult.Success:
                            break;
                        default:
                            throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    }
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
                            throw new QuicRpcException(QuicRpcExceptionType.FunctionNotFound, "QuicRpc function not found at remote.");
                        case QuicRpcExceptionType.RemoteShutdown:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteShutdown, "QuicRpc server shutting down.");
                    }
                }
                throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
            }
        }

        public override async ValueTask InvokeFunctionAsync(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var header = MemoryPool<byte>.Shared.Rent(2))
                {
                    MemoryMarshal.Write(header.Memory.Span, functionId);
                    await stream.WriteAsync(header.Memory.Slice(0, 2), true).ConfigureAwait(false);
                    Debug.WriteLine("Header sent.");
                    var read = await stream.ReadAsync(header.Memory.Slice(0, 2), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc not configure successfully at remote.");
                    if (header.Memory.Span[0] != _Placeholder)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    if (read != 2)
                    {
                        read = await stream.ReadAsync(header.Memory.Slice(read, 2 - read), cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                            throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    }
                    var result = (QuicRpcResult)header.Memory.Span[1];
                    switch (result)
                    {
                        case QuicRpcResult.Streaming:
                        case QuicRpcResult.Response:
                            throw new QuicRpcException(QuicRpcExceptionType.SignatureError, "QuicRpc function signature not equal to remote.");
                        case QuicRpcResult.Success:
                            break;
                        default:
                            throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    }
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
                            throw new QuicRpcException(QuicRpcExceptionType.FunctionNotFound, "QuicRpc function not found at remote.");
                        case QuicRpcExceptionType.RemoteShutdown:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteShutdown, "QuicRpc server shutting down.");
                    }
                }
                throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, ex, "QuicRpc protocol error.");
            }
        }

        public override async ValueTask<QuicStream> InvokeStreamingFunctionAsync(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var header = MemoryPool<byte>.Shared.Rent(2))
                {
                    MemoryMarshal.Write(header.Memory.Span, functionId);
                    await stream.WriteAsync(header.Memory.Slice(0, 2)).ConfigureAwait(false);
                    var read = await stream.ReadAsync(header.Memory.Slice(0, 2), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc not configure successfully at remote.");
                    if (header.Memory.Span[0] != _Placeholder)
                        throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    if (read != 2)
                    {
                        read = await stream.ReadAsync(header.Memory.Slice(read, 2 - read), cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                            throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    }
                    var result = (QuicRpcResult)header.Memory.Span[1];
                    switch (result)
                    {
                        case QuicRpcResult.Success:
                        case QuicRpcResult.Response:
                            throw new QuicRpcException(QuicRpcExceptionType.SignatureError, "QuicRpc function signature not equal to remote.");
                        case QuicRpcResult.Streaming:
                            break;
                        default:
                            throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
                    }
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
                            throw new QuicRpcException(QuicRpcExceptionType.FunctionNotFound, "QuicRpc function not found at remote.");
                        case QuicRpcExceptionType.RemoteShutdown:
                            throw new QuicRpcException(QuicRpcExceptionType.RemoteShutdown, "QuicRpc server shutting down.");
                    }
                }
                throw new QuicRpcException(QuicRpcExceptionType.ProtocolError, "QuicRpc protocol error.");
            }
        }

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
                    var task = StreamHandle(stream, context, exceptionDelegate, cancellationToken).ContinueWith(_ =>
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

        public async Task StreamHandle(QuicStream stream, TContext context, Action<Exception>? exceptionDelegate, CancellationToken cancellationToken)
        {
            try
            {
                using (var header = MemoryPool<byte>.Shared.Rent(2))
                {
                    try
                    {
                        await stream.ReadExactlyAsync(header.Memory.Slice(0, 2), cancellationToken).ConfigureAwait(false);
                        MethodCall func = _functions[MemoryMarshal.Read<ushort>(header.Memory.Span)];
                        if (func == null)
                        {
                            stream.Abort(QuicAbortDirection.Both, (long)QuicRpcExceptionType.FunctionNotFound);
                            return;
                        }
                        header.Memory.Span[0] = _Placeholder;
                        await func(stream, context, header.Memory, cancellationToken).ConfigureAwait(false);
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
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }

        private delegate Task MethodCall(QuicStream stream, TContext context, Memory<byte> header, CancellationToken cancellationToken);
    }
}