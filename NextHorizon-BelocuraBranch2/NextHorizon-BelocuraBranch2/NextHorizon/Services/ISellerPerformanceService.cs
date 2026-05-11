using NextHorizon.Models;

namespace NextHorizon.Services;

public interface ISellerPerformanceService
{
    Task<SellerPerformanceMetrics> GetSellerPerformanceAsync(int sellerId, DateTime today, CancellationToken cancellationToken = default);
    Task<SellerOperationsSummary> GetOperationsSummaryAsync(int sellerId, DateTime today, CancellationToken cancellationToken = default);
    Task<SellerAnalyticsSummary> GetAnalyticsSummaryAsync(int sellerId, DateTime today, CancellationToken cancellationToken = default);
    Task<int[]> GetAvailableRevenueYearsAsync(int sellerId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, List<decimal>>> GetMonthlyRevenueByYearAsync(int sellerId, int[] years, CancellationToken cancellationToken = default);
    Task<Dictionary<int, List<int>>> GetMonthlyOrdersByYearAsync(int sellerId, int[] years, CancellationToken cancellationToken = default);
    Task<Dictionary<int, List<int>>> GetMonthlyUnitsByYearAsync(int sellerId, int[] years, CancellationToken cancellationToken = default);
    Task<List<TopSellingProduct>> GetTopPerformingProductsAsync(int sellerId, int topCount = 5, DateTime? from = null, CancellationToken cancellationToken = default);
    Task<List<CategoryPerformanceViewModel>> GetTopCategoriesAsync(int sellerId, int topCount = 5, DateTime? from = null, CancellationToken cancellationToken = default);
}
