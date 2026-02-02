// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance
    .AddJob(Job.Default
        .WithPlatform(BenchmarkDotNet.Environments.Platform.X64)
        .WithJit(Jit.Default)
        .WithRuntime(CoreRuntime.Core10_0)
        .WithAffinity(1));

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, config);
Console.Read();