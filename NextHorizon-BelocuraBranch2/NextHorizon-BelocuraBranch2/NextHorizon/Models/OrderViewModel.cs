using System;

namespace NextHorizon.Models
{
    public class OrderViewModel
    {
        public string OrderId { get; set; } = string.Empty;
        public string ShopName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Customer { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DateTime { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string FulfillmentStatus { get; set; } = string.Empty;
        public string Courier { get; set; } = string.Empty;
    }
}
