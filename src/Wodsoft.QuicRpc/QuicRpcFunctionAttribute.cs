using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class QuicRpcFunctionAttribute : Attribute
    {
        public QuicRpcFunctionAttribute(byte id)
        {
            Id = id;
        }

        public byte Id { get; }

        public bool IsStreaming { get; set; }
    }
}
