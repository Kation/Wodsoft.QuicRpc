using MemoryPack;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Wodsoft.QuicRpc.Benchmarks
{
    [MemoryPackable]
    public partial class Hello2
    {
        [MemoryPackOrder(0)]
        public string Name { get; set; }
    }
}
