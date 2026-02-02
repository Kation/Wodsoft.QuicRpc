# QuicRpc
QuicRpc是一个基于QUIC协议的高性能远程过程调用（RPC）框架，旨在提供低延迟、高吞吐量和可靠性以及快速开发的解决方案。

为什么选择QuicRpc？
- 低延迟  
基于UDP协议的QUIC在互联网环境下能提供比TCP更低的延迟，从而提升RPC调用的响应速度。
- 高稳定  
QUIC协议提供了稳定可靠的连接管理和拥塞控制，确保在网络波动时依然能保持高效通信。
- 支持P2P调用  
QUIC是基于UDP的协议，支持P2P通信。
- 简单易用  
仅需少量代码即可在现有**QUIC连接**上实现RPC调用。
- 支持AOT  
使用Source Generator生成代码，支持AOT编译。

## 安装

`QuicRpc`通过NuGet进行分发，仅支持`C#`以及`.NET 9`以及后续版本。
```powershell
PM > Install-Package Wodsoft.QuicRpc
```

## 快速开始

### 编写服务端

服务端使用`class`定义服务继承`QuicRpcFunctions`，并使用`[QuicRpcFunction]`特性标记服务类，以及使用`partial`声明。  
`QuicRpcFunction`特性的参数为服务Id，类型是`byte`，所以其取值范围为`0-255`。  
每一个需要被调用的方法也需要使用`[QuicRpcFunction]`特性标记，服务Id不能重复。  
由此意味着，`QuicRpc`服务的最大方法数为**65536**个。

```csharp
[QuicRpcFunction(0)]
public partial class RpcFunctions : QuicRpcFunctions
{
    [QuicRpcFunction(0)]
    public ValueTask Method1(string request)
    {
        return ValueTask.FromResult(request);
    }

    [QuicRpcFunction(1)]
    public ValueTask<Hello> Method2()
    {
        return ValueTask.FromResult(request);
    }

    [QuicRpcFunction(2)]
    public ValueTask<Hello2> Method3(Hello2 request)
    {
        return ValueTask.FromResult(request);
    }
}
```

被调用的方法返回类型必须是`ValueTask<T>`，其中`T`为返回的数据类型，如果不返回数据，返回类型则是`ValueTask`。  
被调用的方法参数必须为空或者仅限一个参数。执行方法时可以通过`GetContext<T>`方法获取上下文。

上下文是一个结构体。  
其中包含了`ConnectionContext`，表示连接该连接对应的上下文。  
`CancellationToken`取消令牌与调用`QuicRpcService.HandleConnection`是同一个令牌。  
`Stream`仅在双向流式调用时有值。
```csharp
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
```

### 编写客户端

客户端使用`struct`定义客户端继承`IQuicRpcClient`接口，并使用`[QuicRpcFunction]`特性标记客户端结构体，以及使用`partial`声明。  
服务Id必须与服务端一致，否则调用时会引发`QuicRpcException`异常。

```csharp
[QuicRpcFunction(0)]
public partial struct RpcClient : IQuicRpcClient
{
    [QuicRpcFunction(0)]
    public partial Task Test(string request);

    [QuicRpcFunction(1)]
    public partial Task<Hello> Test2(CancellationToken cancellationToken = default);

    [QuicRpcFunction(2)]
    public partial Task<Hello2> Test3(Hello2 request, CancellationToken cancellationToken = default);
}
```

调用方法必须定义为部分方法。  
调用方法返回类型必须是`Task<T>`，其中`T`为返回的数据类型，如果不返回数据，返回类型则是`Task`。  
调用方法参数必须为空或者仅限一个参数，且可以额外附加一个`CancellationToken`参数用于取消调用。

### 启动服务端

实例化`QuicRpcService<TContext>`类，并调用`BindFunctions`方法绑定服务类。

```csharp
var quicRpcService = new QuicRpcService<RpcContext>();
quicRpcService.BindFunctions(new RpcFunctions());
```

在**QUIC连接**成功后的适当时机，调用`HandleConnection`方法开始处理RPC数据。

```csharp
quicRpcService.HandleConnection(connection, new RpcContext(), cancellationToken);
```

`HandleConnection`方法会返回一个`Task`，表示该连接的RPC处理的生命周期，直到连接关闭或者发生异常。

### 调用服务端

实例化`RpcClient`客户端结构体，并调用`BindClient`方法绑定**QUIC连接**。  
绑定过后，即可使用`quicRpcClient`进行调用。

```csharp
var quicRpcClient = new RpcClient();
var quicRpcService = new QuicRpcService<RpcContext>();
quicRpcService.BindClient(connection, ref quicRpcClient);
await quicRpcClient.Test("Hello, QuicRpc!");
```

## 序列化

`QuicRpc`默认使用[MemoryPack](https://github.com/Cysharp/MemoryPack)作为序列化框架，开发者应遵循`MemoryPack`的序列化要求。  
开发者可以通过实现`QuicRpcSerializer`抽象类来自定义序列化过程。  
并在实例化`QuicRpcService<TContext>`类时传入自定义的序列化器。
```csharp
var quicRpcService = new QuicRpcService<RpcContext>(new CustomQuicRpcSerializer());
```

## 手动驾驶

### 注册服务

开发者可以通过`QuicRpcService`手动注册服务方法。
```csharp
public class QuicRpcService<TContext>
{
    public void RegisterFunction<TRequest, TResponse>(ushort functionId, Func<QuicRpcContext<TContext>, TRequest, ValueTask<TResponse>> func);

    public void RegisterFunction<TResponse>(ushort functionId, Func<QuicRpcContext<TContext>, ValueTask<TResponse>> func);

    public void RegisterFunction<TRequest>(ushort functionId, Func<QuicRpcContext<TContext>, TRequest, ValueTask> func);

    public void RegisterFunction(ushort functionId, Func<QuicRpcContext<TContext>, ValueTask> func);

    public void RegisterStreamingFunction(ushort functionId, Func<QuicRpcContext<TContext>, ValueTask> func);
}
```

### 调用服务

开发者可以通过`QuicRpcService`手动注册服务方法。

```csharp
public class QuicRpcService<TContext>
{
    public ValueTask<TResponse> InvokeFunctionAsync<TRequest, TResponse>(QuicStream stream, ushort functionId, TRequest request, CancellationToken cancellationToken = default);
    
    public ValueTask<TResponse> InvokeFunctionAsync<TResponse>(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default);
    
    public ValueTask InvokeFunctionAsync<TRequest>(QuicStream stream, ushort functionId, TRequest request, CancellationToken cancellationToken = default);
    
    public ValueTask InvokeFunctionAsync(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default);
    
    public ValueTask<QuicStream> InvokeStreamingFunctionAsync(QuicStream stream, ushort functionId, CancellationToken cancellationToken = default);
}
```

## 双向流

`QuicRpc`支持通过`QuicStream`进行双向流通信。

### 服务端实现

服务端在定义被调用方法时，需要给`QuicRpcFunction`特性设置`IsStreaming`属性为`true`。  
且返回类型必须为`ValueTask`，参数必须为空。

```csharp

[QuicRpcFunction(0)]
public partial class RpcFunctions : QuicRpcFunctions
{
    [QuicRpcFunction(0, IsStreaming = true)]
    public ValueTask StreamingMethod()
    {
        var context = GetContext<RpcContext>();
        var stream = context.Stream;
        context.CancellationToken
        return ValueTask.FromResult(request);
    }
}
```

在方法内可通过上下文里的`Stream`属性获取`QuicStream`对象，从而进行读写操作。  
需要注意的是，方法内操作需要考虑到上下文的**取消令牌**，应在取消时**结束调用**，否则会导致`HandleConnection`任务**无法完成**。  
开发者通常仅需考虑`QuicExpceion`异常的处理。

### 客户端实现

客户端在定义调用方法时，需要给`QuicRpcFunction`特性设置`IsStreaming`属性为`true`。  
且返回类型必须为`Task<QuicStream>`，参数必须为空或者仅限一个`CancellationToken`参数。

```csharp
[QuicRpcFunction(0)]
public partial struct RpcClient : IQuicRpcClient
{
    [QuicRpcFunction(0, IsStreaming = true)]
    public partial Task<QuicStream> Streaming(CancellationToken cancellationToken = default);
}
```

调用方法后会返回一个`QuicStream`对象，从而进行读写操作。  
开发者通常仅需考虑`QuicExpceion`异常的处理。

## 许可

`QuicRpc`使用MIT许可。