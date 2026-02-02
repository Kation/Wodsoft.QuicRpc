using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wodsoft.QuicRpc
{
    public class QuicRpcException : Exception
    {
        public QuicRpcException(QuicRpcExceptionType type, string message) : base(message)
        {
            Type = type;
        }

        public QuicRpcException(QuicRpcExceptionType type, Exception ex, string message) : base(message)
        {
            Type = type;
        }

        public QuicRpcExceptionType Type { get; }
    }
}
