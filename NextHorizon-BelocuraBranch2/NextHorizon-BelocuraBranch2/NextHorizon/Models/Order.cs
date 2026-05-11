using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextHorizon.Models
{
    public class Order
    {
        [Key]
        public int OrderID { get; set; }

        public string? FullName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }

        public string? Status { get; set; } = string.Empty;
        public string? ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }

        public int seller_id { get; set; }
        [Column("ConsumerID")]
        public int? ConsumerID { get; set; }
        public int? logistics_id { get; set; }

        [NotMapped]
        public decimal Amount { get; set; }

        [NotMapped]
        public string Sku { get; set; } = string.Empty;

        [NotMapped]
        public string Size { get; set; } = string.Empty;

        [NotMapped]
        public string ProductImage { get; set; } = string.Empty;

        public string? PaymentMethod { get; set; } = string.Empty;

        [NotMapped]
        public string Courier { get; set; } = string.Empty;

        [NotMapped]
        public string ReturnProofImage => HasReturnProofImage
            ? $"/Dashboard/ReturnProofImage?orderId={OrderID}"
            : string.Empty;

        [NotMapped]
        public bool HasReturnProofImage => ReturnProofImageData is { Length: > 0 };

        public string? ReturnReason { get; set; }
        public string? ReturnNote { get; set; }

        [Column(TypeName = "varbinary(max)")]
        public byte[]? ReturnProofImageData { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        public string? ReturnProofImageMimeType { get; set; }

        public DateTime? ReturnProcessedAt { get; set; }

        public string? CancellationReason { get; set; }

        [NotMapped]
        public decimal CalculatedSubtotal => OrderItems?.Sum(item => item.Quantity * item.UnitPrice) ?? 0;

        [NotMapped]
        public decimal CalculatedTotal => CalculatedSubtotal + ShippingFee;

        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? DeliveryOption { get; set; }

        [NotMapped]
        public string? Colors { get; set; }

        
        [NotMapped]
        public string EffectiveStatus { get; set; } = string.Empty;

        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public string? FulfillmentStatus { get; set; }
        public string? TrackingNumber { get; set; }
        public DateTime? DateShipped { get; set; }
        public string? SellerNote { get; set; }
        public string? ProofOfShipmentUrl { get; set; }
    }
}
