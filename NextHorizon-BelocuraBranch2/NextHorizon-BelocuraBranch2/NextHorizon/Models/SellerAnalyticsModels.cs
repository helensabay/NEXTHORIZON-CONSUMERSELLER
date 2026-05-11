namespace NextHorizon.Models;

public sealed class SellerAnalyticsSummary
{
    public int TodayOrderCount { get; set; }
    public int TotalOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int RefundedOrders { get; set; }
    public int ActiveReturnRequests { get; set; }
    public decimal RefundedRevenue { get; set; }
}

public sealed class CategoryPerformanceViewModel
{
    public string Category { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal RevenueGenerated { get; set; }
    public decimal RevenueShare { get; set; }
}
