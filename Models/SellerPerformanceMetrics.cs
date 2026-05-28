namespace NextHorizon.Models
{
    public sealed class SellerPerformanceMetrics
    {
        public decimal RecognizedRevenueToday { get; set; }
        public decimal YesterdayRecognizedRevenue { get; set; }
        public int RecognizedUnitsSold { get; set; }
        public int RecognizedOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal SalesGrowth { get; set; }
        public decimal CodDeliveredRevenue { get; set; }
        public decimal CodExposure { get; set; }
        public decimal CodSuccessRate { get; set; }
        public int CodRtsCount { get; set; }
    }
}
