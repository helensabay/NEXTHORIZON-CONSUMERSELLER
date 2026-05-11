using System;
using System.Collections.Generic;

namespace NextHorizon.Models
{
    public class OrderDetailsViewModel
    {
        public string SellerName { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public DateTime OrderDateTime { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string FulfillmentStatus { get; set; } = string.Empty;
        public string Courier { get; set; } = string.Empty;
        public string TrackingNumber { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public List<OrderLineItemViewModel> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Discount { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal Total => Subtotal + ShippingFee - Discount;

    }
}
