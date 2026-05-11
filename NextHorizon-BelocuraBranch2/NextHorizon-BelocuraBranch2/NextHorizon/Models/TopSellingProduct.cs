namespace NextHorizon.Models
{
    public class TopSellingProduct
    {
        public string ProductName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int UnitsSold { get; set; }
        public decimal RevenueGenerated { get; set; }
        public int Rank { get; set; }
    }
}
