using System.ComponentModel.DataAnnotations.Schema;

namespace NextHorizon.Models
{
    public partial class ReturnRequest
    {
        public string ResolutionType { get; set; } = "Refund";

        [ForeignKey(nameof(ReplacementOrder))]
        public int? ReplacementOrderId { get; set; }

        public Order? ReplacementOrder { get; set; }
    }
}
