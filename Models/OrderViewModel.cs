namespace MyAspNetApp.Models
{
    public class OrderViewModel
    {
        public required string OrderNumber { get; set; }
        public required string Status { get; set; }
        public required string ProductName { get; set; }
        public required string ProductImage { get; set; }
        public required string PaymentMethod { get; set; }

        public string SellerName { get; set; } = "Monochrome Official Store";
        public string ReceiverName { get; set; } = "Juan Dela Cruz";
        public string PhoneNumber { get; set; } = "(+63) 912 345 6789";
        public string ShippingAddress { get; set; } = "123 Street Name, Barangay, Cebu City, Philippines, 6000";

        public bool IsShippingLocked => Status == "To Ship" || Status == "To Receive" || Status == "Completed";

        public DateTime OrderDate { get; set; }
        public string? EstimatedArrival { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
