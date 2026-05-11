using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace NextHorizon.Models
{
    [Table("OrderItems")]
    public class OrderItem
    {
        [Key]
        public int OrderItemID { get; set; }
        public int OrderID { get; set; }
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public int? VariantId { get; set; }

        [NotMapped]
        public string? Sku { get; set; }

        public int? SellerId { get; set; }

        [ForeignKey("OrderID")]
        [JsonIgnore]
        public Order? Order { get; set; }

        [ForeignKey("ProductID")]
        public DbProduct? Product { get; set; }
    }
}
