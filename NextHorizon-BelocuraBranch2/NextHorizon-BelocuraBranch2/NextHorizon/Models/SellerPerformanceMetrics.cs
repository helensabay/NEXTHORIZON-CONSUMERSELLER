namespace NextHorizon.Models;

public sealed class SellerPerformanceMetrics
{
    public decimal RecognizedRevenueToday { get; set; }
    public decimal YesterdayRecognizedRevenue { get; set; }
    public int RecognizedUnitsSold { get; set; }
    public int RecognizedOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal SalesGrowth { get; set; }
}
