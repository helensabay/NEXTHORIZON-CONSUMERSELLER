namespace NextHorizon.Models
{
    public class Order
    {
        public int Id { get; set; }

        // Adding '?' makes these nullable so the "Constructor" warning goes away
        public string? ShopName { get; set; }
        public string? ProductName { get; set; }
        public string? ProductDetails { get; set; }
        public string? ImageUrl { get; set; }

        // Ensure these are NOT strings in the model
        public decimal Price { get; set; }
        public int Quantity { get; set; }

        public string? Status { get; set; } // "To Pay", "To Ship", etc.
        public bool IsApproved { get; set; }
    }
}