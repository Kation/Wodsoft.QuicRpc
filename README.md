# QuicRpc
`QuicRpc`是一个基于**QUIC**协议的高性能远程过程调用（RPC）框架，旨在提供低延迟、高吞吐量和可靠性以及快速开发的解决方案。

为什么选择QuicRpc？
- 低延迟  
基于**UDP**协议的**QUIC**在互联网环境下能提供比**TCP**更低的延迟，从而提升RPC调用的响应速度。
- 高稳定  
**QUIC**协议提供了稳定可靠的连接管理和拥塞控制，确保在网络波动时依然能保持高效通信。
- 支持P2P调用  
**QUIC**是基于**UDP**的协议，支持**P2P**通信。
- 简单易用  
仅需少量代码即可在现有**QUIC连接**上实现RPC调用。
- 支持AOT  
使用**Source Generator**生成代码，支持AOT编译。

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

`HandleConnection`方法会返回一个`Task`，表示该连接的RPC处理任务。  
方法内会循环接受新的`QuicStream`，直到连接关闭或者发生异常。

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

### 共享Quic连接

如果Quic连接不仅仅用于QuicRpc时，开发者可以不调用`HandleConnection`，而是自行管理连入的`QuicStream`。  
通过增加`QuicStream`的头部数据，当满足条件的时候，再调用`HandleStream`处理RPC业务。  
此时客户端将无法通过**Source Generator**生成的代码进行调用，应自行打开出口`QuicStream`，写入头部数据后再调用`InvokeFunctionAsync`方法进行调用。

```csharp
public class QuicRpcService<TContext>
{
    public Task HandleStream(QuicStream stream, TContext context, Action<Exception>? exceptionDelegate, CancellationToken cancellationToken);
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

## 性能

使用无请求参数、无返回值的方法进行测试，排除序列化差异问题。  
`Batch`为一轮测试的总请求数，`Thread`为本轮测试中并发请求线程数。

### 本机127.0.0.1环回测试

i9-13900HK物理机，服务端与客户端均运行在测试进程里。

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26100.4061/24H2/2024Update/HudsonValley)
13th Gen Intel Core i9-13900HK 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3
  Job-YOFBDC : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3

Affinity=00000000000000000001  Jit=Default  Platform=X64  
Runtime=.NET 10.0  

| Method       | Batch | Thread | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0      | Gen1      | Allocated | Alloc Ratio |
|------------- |------ |------- |----------:|----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|------------:|
| QuicRpc      | 1000  | 1      | 107.92 ms |  2.518 ms |  7.183 ms | 107.88 ms |  1.00 |    0.09 |  200.0000 |         - |   3.44 MB |        1.00 |
| Grpc         | 1000  | 1      |  50.79 ms |  3.913 ms | 10.646 ms |  51.98 ms |  0.47 |    0.10 |  500.0000 |         - |   8.45 MB |        2.46 |
| GrpcWithQuic | 1000  | 1      | 210.71 ms | 20.510 ms | 60.153 ms | 204.65 ms |  1.96 |    0.57 | 1000.0000 |         - |  13.62 MB |        3.96 |
|              |       |        |           |           |           |           |       |         |           |           |           |             |
| QuicRpc      | 1000  | 8      |  14.51 ms |  0.286 ms |  0.585 ms |  14.37 ms |  1.00 |    0.06 |  281.2500 |         - |    3.5 MB |        1.00 |
| Grpc         | 1000  | 8      |  36.49 ms |  0.718 ms |  1.514 ms |  36.64 ms |  2.52 |    0.14 |  700.0000 |  100.0000 |   8.87 MB |        2.54 |
| GrpcWithQuic | 1000  | 8      |  75.84 ms |  4.328 ms | 11.921 ms |  75.43 ms |  5.24 |    0.84 | 1000.0000 |         - |  14.09 MB |        4.03 |
|              |       |        |           |           |           |           |       |         |           |           |           |             |
| QuicRpc      | 5000  | 8      |  73.24 ms |  1.448 ms |  3.022 ms |  73.09 ms |  1.00 |    0.06 | 1333.3333 |         - |   17.5 MB |        1.00 |
| Grpc         | 5000  | 8      | 145.50 ms | 11.354 ms | 33.477 ms | 147.39 ms |  1.99 |    0.46 | 3500.0000 |  500.0000 |  44.11 MB |        2.52 |
| GrpcWithQuic | 5000  | 8      | 362.54 ms | 27.837 ms | 78.515 ms | 368.78 ms |  4.96 |    1.09 | 5000.0000 | 1000.0000 |  70.85 MB |        4.05 |
|              |       |        |           |           |           |           |       |         |           |           |           |             |
| QuicRpc      | 5000  | 16     |  48.10 ms |  1.030 ms |  2.989 ms |  47.67 ms |  1.00 |    0.09 | 1375.0000 |  125.0000 |  17.77 MB |        1.00 |
| Grpc         | 5000  | 16     | 111.32 ms |  6.134 ms | 17.199 ms | 115.39 ms |  2.32 |    0.38 | 3666.6667 |  666.6667 |  45.34 MB |        2.55 |
| GrpcWithQuic | 5000  | 16     | 281.81 ms | 18.255 ms | 53.826 ms | 281.31 ms |  5.88 |    1.18 | 6000.0000 | 2000.0000 |  72.67 MB |        4.09 |
|              |       |        |           |           |           |           |       |         |           |           |           |             |
| QuicRpc      | 5000  | 32     |  39.92 ms |  1.597 ms |  4.609 ms |  38.78 ms |  1.01 |    0.16 | 1500.0000 |  333.3333 |  17.99 MB |        1.00 |
| Grpc         | 5000  | 32     |  61.13 ms |  7.140 ms | 20.939 ms |  52.44 ms |  1.55 |    0.56 | 3666.6667 | 1000.0000 |  45.82 MB |        2.55 |
| GrpcWithQuic | 5000  | 32     | 215.39 ms | 11.894 ms | 34.882 ms | 205.20 ms |  5.46 |    1.07 | 6000.0000 | 3000.0000 |  79.25 MB |        4.41 |

### 有线局域网测试

客户端运行在i7-8086K物理机里，服务端运行在i7-13700K的6大核虚拟机里。

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7705/25H2/2025Update/HudsonValley2)
Intel Core i7-8086K CPU 4.00GHz (Max: 4.01GHz) (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  Job-TEDELB : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3

Affinity=000000000001  Jit=Default  Platform=X64
Runtime=.NET 10.0

| Method       | Batch | Thread | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0      | Gen1      | Allocated | Alloc Ratio |
|------------- |------ |------- |----------:|----------:|----------:|----------:|------:|--------:|----------:|----------:|----------:|------------:|
| QuicRpc      | 1000  | 1      | 530.58 ms | 22.989 ms | 66.328 ms | 514.27 ms |  1.01 |    0.17 |         - |         - |   1.57 MB |        1.00 |
| Grpc         | 1000  | 1      | 599.26 ms | 28.481 ms | 81.718 ms | 609.39 ms |  1.15 |    0.20 | 1000.0000 |         - |   6.75 MB |        4.29 |
| GrpcWithQuic | 1000  | 1      | 535.40 ms | 19.209 ms | 56.033 ms | 520.64 ms |  1.02 |    0.16 | 1000.0000 |         - |   8.51 MB |        5.41 |
|              |       |        |           |           |           |           |       |         |           |           |           |             |
| QuicRpc      | 1000  | 8      |  71.88 ms |  3.197 ms |  9.325 ms |  68.71 ms |  1.02 |    0.18 |  142.8571 |         - |   1.68 MB |        1.00 |
| Grpc         | 1000  | 8      |  68.49 ms |  4.696 ms | 13.697 ms |  67.26 ms |  0.97 |    0.23 | 1000.0000 |         - |   6.85 MB |        4.08 |
| GrpcWithQuic | 1000  | 8      | 183.89 ms |  7.056 ms | 19.433 ms | 185.50 ms |  2.60 |    0.41 | 1250.0000 |         - |   8.78 MB |        5.22 |
|              |       |        |           |           |           |           |       |         |           |           |           |             |
| QuicRpc      | 5000  | 8      | 331.50 ms | 12.477 ms | 35.799 ms | 329.20 ms |  1.01 |    0.15 | 1000.0000 |         - |    8.4 MB |        1.00 |
| Grpc         | 5000  | 8      | 370.08 ms | 22.818 ms | 66.923 ms | 368.73 ms |  1.13 |    0.24 | 5000.0000 |         - |  34.25 MB |        4.08 |
| GrpcWithQuic | 5000  | 8      | 594.91 ms | 29.748 ms | 85.351 ms | 576.52 ms |  1.81 |    0.32 | 7000.0000 | 1000.0000 |  43.78 MB |        5.21 |
|              |       |        |           |           |           |           |       |         |           |           |           |             |
| QuicRpc      | 5000  | 16     | 287.07 ms | 15.781 ms | 45.530 ms | 271.75 ms |  1.02 |    0.22 | 1333.3333 |         - |    8.4 MB |        1.00 |
| Grpc         | 5000  | 16     | 203.60 ms | 12.521 ms | 35.925 ms | 194.00 ms |  0.73 |    0.16 | 5000.0000 | 1000.0000 |  34.25 MB |        4.08 |
| GrpcWithQuic | 5000  | 16     | 389.70 ms | 26.410 ms | 75.775 ms | 384.87 ms |  1.39 |    0.33 | 7000.0000 | 1000.0000 |  43.82 MB |        5.22 |
|              |       |        |           |           |           |           |       |         |           |           |           |             |
| QuicRpc      | 5000  | 32     | 157.01 ms |  6.633 ms | 19.137 ms | 154.19 ms |  1.01 |    0.17 | 1250.0000 |         - |    8.4 MB |        1.00 |
| Grpc         | 5000  | 32     | 215.26 ms | 11.382 ms | 32.472 ms | 208.87 ms |  1.39 |    0.27 | 5000.0000 | 1000.0000 |  34.25 MB |        4.08 |
| GrpcWithQuic | 5000  | 32     | 389.84 ms | 26.227 ms | 73.975 ms | 383.39 ms |  2.52 |    0.56 | 7000.0000 | 2000.0000 |  43.25 MB |        5.15 |

### 模拟无丢包互联网测试

客户端运行在i7-8086K物理机里，服务端运行在i7-13700K的6大核虚拟机里。  
服务端使用[clumsy](https://github.com/jagt/clumsy)模拟互联网，启用了Lag的Inbound、Delay 16ms选项。  
实际Ping测试平均最低延迟17ms，最高延迟58ms，平均35ms。

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7705/25H2/2025Update/HudsonValley2)
Intel Core i7-8086K CPU 4.00GHz (Max: 4.01GHz) (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  Job-TEDELB : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3

Affinity=000000000001  Jit=Default  Platform=X64
Runtime=.NET 10.0

| Method       | Batch | Thread | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Gen0      | Gen1     | Allocated  | Alloc Ratio |
|------------- |------ |------- |---------:|---------:|---------:|---------:|------:|--------:|----------:|---------:|-----------:|------------:|
| QuicRpc      | 100   | 8      | 525.6 ms | 10.51 ms | 23.71 ms | 531.0 ms |  1.00 |    0.07 |         - |        - |  174.61 KB |        1.00 |
| Grpc         | 100   | 8      | 525.3 ms | 10.31 ms | 14.12 ms | 530.5 ms |  1.00 |    0.06 |         - |        - |  704.41 KB |        4.03 |
| GrpcWithQuic | 100   | 8      | 525.4 ms | 10.33 ms | 21.34 ms | 531.4 ms |  1.00 |    0.06 |         - |        - |  924.71 KB |        5.30 |
|              |       |        |          |          |          |          |       |         |           |          |            |             |
| QuicRpc      | 100   | 16     | 281.6 ms |  5.61 ms | 11.07 ms | 286.1 ms |  1.00 |    0.06 |         - |        - |  175.03 KB |        1.00 |
| Grpc         | 100   | 16     | 279.8 ms |  5.55 ms | 10.69 ms | 285.7 ms |  1.00 |    0.06 |         - |        - |  705.07 KB |        4.03 |
| GrpcWithQuic | 100   | 16     | 286.1 ms |  5.62 ms | 11.23 ms | 287.0 ms |  1.02 |    0.06 |         - |        - |  914.06 KB |        5.22 |
|              |       |        |          |          |          |          |       |         |           |          |            |             |
| QuicRpc      | 100   | 32     | 161.5 ms |  3.16 ms |  5.02 ms | 163.5 ms |  1.00 |    0.04 |         - |        - |  176.62 KB |        1.00 |
| Grpc         | 100   | 32     | 162.1 ms |  3.22 ms |  5.11 ms | 163.9 ms |  1.00 |    0.05 |         - |        - |  706.59 KB |        4.00 |
| GrpcWithQuic | 100   | 32     | 163.6 ms |  3.19 ms |  5.58 ms | 162.8 ms |  1.01 |    0.05 |         - |        - |  954.14 KB |        5.40 |
|              |       |        |          |          |          |          |       |         |           |          |            |             |
| QuicRpc      | 500   | 64     | 324.6 ms |  6.45 ms | 10.24 ms | 327.3 ms |  1.00 |    0.05 |         - |        - |     878 KB |        1.00 |
| Grpc         | 500   | 64     | 331.2 ms |  6.54 ms | 13.22 ms | 328.8 ms |  1.02 |    0.05 | 1000.0000 | 500.0000 | 6531.71 KB |        7.44 |
| GrpcWithQuic | 500   | 64     | 410.8 ms |  8.19 ms | 20.09 ms | 414.8 ms |  1.27 |    0.07 |         - |        - | 4900.23 KB |        5.58 |
|              |       |        |          |          |          |          |       |         |           |          |            |             |
| QuicRpc      | 500   | 128    | 162.6 ms |  3.15 ms |  5.52 ms | 164.1 ms |  1.00 |    0.05 |         - |        - |  965.58 KB |        1.00 |
| Grpc         | 500   | 128    | 168.3 ms |  3.34 ms |  8.63 ms | 167.9 ms |  1.04 |    0.06 |  666.6667 | 333.3333 | 4990.55 KB |        5.17 |
| GrpcWithQuic | 500   | 128    | 414.0 ms |  9.17 ms | 26.61 ms | 413.4 ms |  2.55 |    0.19 | 1000.0000 |        - | 8828.54 KB |        9.14 |

### 模拟丢包率1%互联网测试

客户端运行在i7-8086K物理机里，服务端运行在i7-13700K的6大核虚拟机里。  
服务端使用[clumsy](https://github.com/jagt/clumsy)模拟互联网，启用了Lag（Inbound - Delay 16ms）选项，启用了Drop（Inbound、Outbound - Chance 1%）选项。  
实际Ping测试平均最低延迟17ms，最高延迟58ms，平均35ms。

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7705/25H2/2025Update/HudsonValley2)
Intel Core i7-8086K CPU 4.00GHz (Max: 4.01GHz) (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  Job-TEDELB : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3

Affinity=000000000001  Jit=Default  Platform=X64
Runtime=.NET 10.0

| Method       | Batch | Thread | Mean     | Error    | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated  | Alloc Ratio |
|------------- |------ |------- |---------:|---------:|----------:|------:|--------:|----------:|----------:|-----------:|------------:|
| QuicRpc      | 100   | 8      | 537.1 ms | 10.71 ms |  23.96 ms |  1.00 |    0.06 |         - |         - |  173.44 KB |        1.00 |
| Grpc         | 100   | 8      | 603.6 ms | 25.20 ms |  71.50 ms |  1.13 |    0.14 |         - |         - |   703.2 KB |        4.05 |
| GrpcWithQuic | 100   | 8      | 579.5 ms | 21.85 ms |  63.74 ms |  1.08 |    0.13 |         - |         - |  927.55 KB |        5.35 |
|              |       |        |          |          |           |       |         |           |           |            |             |
| QuicRpc      | 100   | 16     | 285.9 ms |  5.37 ms |  12.86 ms |  1.00 |    0.06 |         - |         - |  175.77 KB |        1.00 |
| Grpc         | 100   | 16     | 334.1 ms | 14.43 ms |  41.65 ms |  1.17 |    0.16 |         - |         - |  708.23 KB |        4.03 |
| GrpcWithQuic | 100   | 16     | 299.3 ms |  7.83 ms |  23.10 ms |  1.05 |    0.09 |         - |         - |  947.09 KB |        5.39 |
|              |       |        |          |          |           |       |         |           |           |            |             |
| QuicRpc      | 100   | 32     | 164.8 ms |  3.28 ms |   7.19 ms |  1.00 |    0.06 |         - |         - |   176.4 KB |        1.00 |
| Grpc         | 100   | 32     | 197.8 ms |  8.63 ms |  24.06 ms |  1.20 |    0.15 |         - |         - |  706.22 KB |        4.00 |
| GrpcWithQuic | 100   | 32     | 170.2 ms |  4.23 ms |  12.14 ms |  1.03 |    0.09 |         - |         - |  945.05 KB |        5.36 |
|              |       |        |          |          |           |       |         |           |           |            |             |
| QuicRpc      | 500   | 64     | 343.5 ms |  6.86 ms |  18.90 ms |  1.00 |    0.08 |         - |         - | 1128.66 KB |        1.00 |
| Grpc         | 500   | 64     | 517.5 ms | 37.95 ms | 111.31 ms |  1.51 |    0.33 | 1000.0000 |         - | 7588.97 KB |        6.72 |
| GrpcWithQuic | 500   | 64     | 445.7 ms | 11.33 ms |  32.68 ms |  1.30 |    0.12 |         - |         - | 4788.77 KB |        4.24 |
|              |       |        |          |          |           |       |         |           |           |            |             |
| QuicRpc      | 500   | 128    | 183.6 ms |  4.39 ms |  12.89 ms |  1.00 |    0.10 |         - |         - | 1201.89 KB |        1.00 |
| Grpc         | 500   | 128    | 331.5 ms | 17.92 ms |  52.28 ms |  1.81 |    0.31 | 1333.3333 | 1000.0000 | 8922.82 KB |        7.42 |
| GrpcWithQuic | 500   | 128    | 447.5 ms | 10.65 ms |  31.25 ms |  2.45 |    0.24 |         - |         - | 5622.51 KB |        4.68 |

### 模拟丢包率3%互联网测试

客户端运行在i7-8086K物理机里，服务端运行在i7-13700K的6大核虚拟机里。  
服务端使用[clumsy](https://github.com/jagt/clumsy)模拟互联网，启用了Lag（Inbound - Delay 16ms）选项，启用了Drop（Inbound、Outbound - Chance 3%）选项。  
实际Ping测试平均最低延迟17ms，最高延迟58ms，平均35ms。

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7705/25H2/2025Update/HudsonValley2)
Intel Core i7-8086K CPU 4.00GHz (Max: 4.01GHz) (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  Job-TEDELB : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3

Affinity=000000000001  Jit=Default  Platform=X64
Runtime=.NET 10.0

| Method       | Batch | Thread | Mean     | Error    | StdDev    | Median   | Ratio | RatioSD | Gen0      | Allocated  | Alloc Ratio |
|------------- |------ |------- |---------:|---------:|----------:|---------:|------:|--------:|----------:|-----------:|------------:|
| QuicRpc      | 100   | 8      | 605.6 ms | 24.18 ms |  69.39 ms | 583.0 ms |  1.01 |    0.16 |         - |  174.61 KB |        1.00 |
| Grpc         | 100   | 8      | 788.7 ms | 63.84 ms | 187.23 ms | 740.5 ms |  1.32 |    0.34 |         - |  705.03 KB |        4.04 |
| GrpcWithQuic | 100   | 8      | 663.8 ms | 26.97 ms |  78.23 ms | 655.8 ms |  1.11 |    0.18 |         - |  928.43 KB |        5.32 |
|              |       |        |          |          |           |          |       |         |           |            |             |
| QuicRpc      | 100   | 16     | 310.5 ms |  9.11 ms |  26.73 ms | 307.6 ms |  1.01 |    0.12 |         - |  175.25 KB |        1.00 |
| Grpc         | 100   | 16     | 452.2 ms | 34.43 ms |  99.88 ms | 430.0 ms |  1.47 |    0.35 |         - |  704.91 KB |        4.02 |
| GrpcWithQuic | 100   | 16     | 340.9 ms | 17.20 ms |  49.89 ms | 329.6 ms |  1.11 |    0.19 |         - |  930.57 KB |        5.31 |
|              |       |        |          |          |           |          |       |         |           |            |             |
| QuicRpc      | 100   | 32     | 176.8 ms |  7.19 ms |  20.98 ms | 176.8 ms |  1.01 |    0.17 |         - |  177.09 KB |        1.00 |
| Grpc         | 100   | 32     | 299.4 ms | 29.69 ms |  86.13 ms | 286.5 ms |  1.72 |    0.53 |         - |  706.82 KB |        3.99 |
| GrpcWithQuic | 100   | 32     | 198.9 ms |  8.55 ms |  24.94 ms | 196.9 ms |  1.14 |    0.20 |         - |  929.61 KB |        5.25 |
|              |       |        |          |          |           |          |       |         |           |            |             |
| QuicRpc      | 500   | 64     | 384.1 ms | 15.59 ms |  45.73 ms | 372.7 ms |  1.01 |    0.17 |         - |  936.51 KB |        1.00 |
| Grpc         | 500   | 64     | 768.2 ms | 57.46 ms | 162.07 ms | 742.7 ms |  2.03 |    0.49 |         - | 4672.13 KB |        4.99 |
| GrpcWithQuic | 500   | 64     | 527.6 ms | 23.79 ms |  69.38 ms | 525.6 ms |  1.39 |    0.24 |         - | 4727.98 KB |        5.05 |
|              |       |        |          |          |           |          |       |         |           |            |             |
| QuicRpc      | 500   | 128    | 216.2 ms | 10.51 ms |  29.98 ms | 210.1 ms |  1.02 |    0.20 |         - |  944.82 KB |        1.00 |
| Grpc         | 500   | 128    | 545.3 ms | 39.25 ms | 112.62 ms | 528.1 ms |  2.57 |    0.64 | 1000.0000 | 9377.88 KB |        9.93 |
| GrpcWithQuic | 500   | 128    | 516.9 ms | 21.93 ms |  64.31 ms | 511.3 ms |  2.44 |    0.45 |         - |  4862.3 KB |        5.15 |

## 许可

`QuicRpc`使用MIT许可。