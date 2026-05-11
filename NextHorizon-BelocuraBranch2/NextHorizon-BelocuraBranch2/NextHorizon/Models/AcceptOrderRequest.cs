using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NextHorizon.Models
{
    public class AcceptOrderRequest
    {
        public int OrderId { get; set; }
        public int Courier { get; set; }
    }

    public class AcceptOrderResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
