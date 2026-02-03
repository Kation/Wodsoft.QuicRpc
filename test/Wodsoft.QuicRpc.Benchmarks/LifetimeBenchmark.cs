using BenchmarkDotNet.Attributes;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftAntimalwareAMFilter;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static BenchmarkDotNet.Engines.EngineEventSource;

namespace Wodsoft.QuicRpc.Benchmarks
{
    public class LifetimeBenchmark
    {
        private volatile int _index;

        public IEnumerable<object[]> Parameters()
        {
            yield return new object[] { 1000, 1 };
            yield return new object[] { 1000, 8 };
            yield return new object[] { 10000, 8 };
            yield return new object[] { 10000, 16 };
            yield return new object[] { 10000, 32 };
        }

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(Parameters))]
        public void Kestrel(int batch, int thread)
        {
            KestrelLifetime lifetime = new KestrelLifetime();
            if (thread == 1)
            {
                for (int i = 0; i < batch; i++)
                {
                    var request = CreateRequest();
                    lifetime.Add(request);
                    lifetime.Remove(request);
                }
            }
            else
            {
                Parallel.For(0, batch, new ParallelOptions { MaxDegreeOfParallelism = thread }, (_, _) =>
                {
                    var request = CreateRequest();
                    lifetime.Add(request);
                    lifetime.Remove(request);
                });
            }
        }

        [Benchmark]
        [ArgumentsSource(nameof(Parameters))]
        public void Mole(int batch, int thread)
        {
            MoleLifetime lifetime = new MoleLifetime();
            if (thread == 1)
            {
                for (int i = 0; i < batch; i++)
                {
                    var request = CreateRequest();
                    lifetime.Add(request);
                    lifetime.Remove(request);
                }
            }
            else
            {
                Parallel.For(0, batch, new ParallelOptions { MaxDegreeOfParallelism = thread }, (_, _) =>
                {
                    var request = CreateRequest();
                    lifetime.Add(request);
                    lifetime.Remove(request);
                });
            }
        }

        private Request CreateRequest()
        {
            var index = Interlocked.Add(ref _index, 1);
            Request request = new Request
            {
                Id = index
            };
            return request;
        }

        private interface ILifetime
        {
            void Add(Request request);

            void Remove(Request request);
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct Request
        {
            [FieldOffset(0)]
            public int Id;
            [FieldOffset(4)]
            public int Index;
        }

        private class KestrelLifetime : ILifetime
        {
            private Lock _lock = new Lock();
            private Dictionary<int, Request> _request = new Dictionary<int, Request>();

            public void Add(Request request)
            {
                _lock.Enter();
                _request.Add(request.Id, request);
                _lock.Exit();
            }

            public void Remove(Request request)
            {
                _lock.Enter();
                _request.Remove(request.Id);
                _lock.Exit();
            }
        }

        private class MoleLifetime : ILifetime
        {
            private int _count;
            private TaskCompletionSource _tcs;

            public void Add(Request request)
            {
                Interlocked.Increment(ref _count);
            }

            public void Remove(Request request)
            {
                var count = Interlocked.Decrement(ref _count);
                if (count == 0)
                    Volatile.Read(ref _tcs)?.SetResult();
            }

            public Task WaitAllAsync()
            {
                if (Volatile.Read(ref _count) == 0)
                    return Task.CompletedTask;
                var tcs = new TaskCompletionSource();
                Volatile.Write(ref _tcs, tcs);
                if (Volatile.Read(ref _count) == 0)
                    return Task.CompletedTask;
                return _tcs.Task;
            }
        }
    }
}
