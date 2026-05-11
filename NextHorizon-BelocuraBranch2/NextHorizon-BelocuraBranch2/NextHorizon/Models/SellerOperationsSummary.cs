namespace NextHorizon.Models;

public sealed class SellerOperationsSummary
{
    public int TodayOrderCount { get; set; }
    public int TodayUnitsSold { get; set; }
    public decimal TodayOrderValue { get; set; }
    public decimal YesterdayOrderValue { get; set; }
    public int TotalOrderCount { get; set; }
    public int TotalUnitsSold { get; set; }
    public decimal TotalOrderValue { get; set; }
    public int OpenOrderCount { get; set; }
    public int ShippedTodayCount { get; set; }
    public int InFulfillmentCount { get; set; }
    public decimal PipelineRevenue { get; set; }
}
