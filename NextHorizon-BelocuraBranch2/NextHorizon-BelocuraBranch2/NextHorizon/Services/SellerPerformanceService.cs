using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NextHorizon.Data;
using NextHorizon.Models;

namespace NextHorizon.Services;

public sealed class SellerPerformanceService : ISellerPerformanceService
{
    private const string RevenueStatusesSql = "'Completed', 'Delivered'";
    private static readonly string[] RevenueStatuses = ["Completed", "Delivered"];
    private static readonly string[] ActiveReturnStatuses = ["Return Requested", "Return Approved", "Item Returned"];
    private static readonly string[] InFulfillmentStatuses = ["To Ship", "Shipped"];
    private static readonly string[] CancelledStatuses = ["Cancelled", "Failed Delivery"];
    private readonly string _connectionString;
    private readonly AppDbContext _context;
    private readonly ILogger<SellerPerformanceService> _logger;

    public SellerPerformanceService(
        IConfiguration configuration,
        AppDbContext context,
        ILogger<SellerPerformanceService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        _context = context;
        _logger = logger;
    }

    public async Task<SellerPerformanceMetrics> GetSellerPerformanceAsync(
        int sellerId,
        DateTime today,
        CancellationToken cancellationToken = default)
    {
        if (sellerId <= 0 || string.IsNullOrWhiteSpace(_connectionString))
        {
            return new SellerPerformanceMetrics();
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand("dbo.sp_GetSellerPerformanceMetrics", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@SellerId", sellerId);
            command.Parameters.AddWithValue("@Today", today.Date);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new SellerPerformanceMetrics();
            }

            return new SellerPerformanceMetrics
            {
                RecognizedRevenueToday = reader["TodaySales"] is DBNull ? 0m : Convert.ToDecimal(reader["TodaySales"]),
                YesterdayRecognizedRevenue = reader["YesterdaySales"] is DBNull ? 0m : Convert.ToDecimal(reader["YesterdaySales"]),
                RecognizedUnitsSold = reader["TotalUnitsSold"] is DBNull ? 0 : Convert.ToInt32(reader["TotalUnitsSold"]),
                RecognizedOrders = reader["RecognizedOrders"] is DBNull ? 0 : Convert.ToInt32(reader["RecognizedOrders"]),
                TotalRevenue = reader["TotalRevenue"] is DBNull ? 0m : Convert.ToDecimal(reader["TotalRevenue"]),
                SalesGrowth = reader["SalesGrowth"] is DBNull ? 0m : Convert.ToDecimal(reader["SalesGrowth"])
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch seller performance metrics for seller {SellerId}. Returning defaults.", sellerId);
            return new SellerPerformanceMetrics();
        }
    }

    public async Task<SellerOperationsSummary> GetOperationsSummaryAsync(
        int sellerId,
        DateTime today,
        CancellationToken cancellationToken = default)
    {
        var summary = new SellerOperationsSummary();

        if (sellerId <= 0)
        {
            return summary;
        }

        try
        {
            var todayStart = today.Date;
            var tomorrowStart = todayStart.AddDays(1);
            var yesterdayStart = todayStart.AddDays(-1);

            var nonCancelledOrders = _context.Orders
                .AsNoTracking()
                .Where(order => order.seller_id == sellerId && !CancelledStatuses.Contains(order.Status ?? string.Empty));

            summary.TodayOrderCount = await nonCancelledOrders
                .CountAsync(order => order.OrderDate >= todayStart && order.OrderDate < tomorrowStart, cancellationToken);

            summary.TodayUnitsSold = await nonCancelledOrders
                .Where(order => order.OrderDate >= todayStart && order.OrderDate < tomorrowStart)
                .SumAsync(order => (int?)order.Quantity, cancellationToken) ?? 0;

            summary.TodayOrderValue = await nonCancelledOrders
                .Where(order => order.OrderDate >= todayStart && order.OrderDate < tomorrowStart)
                .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;

            summary.YesterdayOrderValue = await nonCancelledOrders
                .Where(order => order.OrderDate >= yesterdayStart && order.OrderDate < todayStart)
                .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;

            summary.TotalOrderCount = await nonCancelledOrders.CountAsync(cancellationToken);

            summary.TotalUnitsSold = await nonCancelledOrders
                .SumAsync(order => (int?)order.Quantity, cancellationToken) ?? 0;

            summary.TotalOrderValue = await nonCancelledOrders
                .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;

            summary.ShippedTodayCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(order =>
                    order.seller_id == sellerId
                    && order.DateShipped != null
                    && order.DateShipped >= todayStart
                    && order.DateShipped < tomorrowStart,
                    cancellationToken);

            summary.InFulfillmentCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(order =>
                    order.seller_id == sellerId
                    && InFulfillmentStatuses.Contains(order.Status ?? string.Empty),
                    cancellationToken);

            summary.OpenOrderCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(order =>
                    order.seller_id == sellerId
                    && !CancelledStatuses.Contains(order.Status ?? string.Empty)
                    && !RevenueStatuses.Contains(order.Status ?? string.Empty),
                    cancellationToken);

            summary.PipelineRevenue = await _context.Orders
                .AsNoTracking()
                .Where(order =>
                    order.seller_id == sellerId
                    && !CancelledStatuses.Contains(order.Status ?? string.Empty)
                    && !RevenueStatuses.Contains(order.Status ?? string.Empty))
                .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch operations summary for seller {SellerId}. Returning defaults.", sellerId);
        }

        return summary;
    }

    public async Task<SellerAnalyticsSummary> GetAnalyticsSummaryAsync(
        int sellerId,
        DateTime today,
        CancellationToken cancellationToken = default)
    {
        var summary = new SellerAnalyticsSummary();

        if (sellerId <= 0)
        {
            return summary;
        }

        try
        {
            summary.TotalOrders = await _context.Orders
                .AsNoTracking()
                .CountAsync(order => order.seller_id == sellerId && RevenueStatuses.Contains(order.Status ?? string.Empty), cancellationToken);

            summary.CancelledOrders = await _context.Orders
                .AsNoTracking()
                .CountAsync(order => order.seller_id == sellerId && CancelledStatuses.Contains(order.Status ?? string.Empty), cancellationToken);

            var latestReturns = await _context.ReturnRequests
                .AsNoTracking()
                .Where(request => request.SellerId == sellerId)
                .OrderByDescending(request => request.UpdatedAt)
                .ThenByDescending(request => request.CreatedAt)
                .ToListAsync(cancellationToken);

            var latestReturnByOrderId = latestReturns
                .GroupBy(request => request.OrderId)
                .ToDictionary(group => group.Key, group => group.First().Status ?? string.Empty);

            var refundedOrderIds = latestReturnByOrderId
                .Where(entry => string.Equals(entry.Value, "Refunded", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Key)
                .ToArray();

            summary.RefundedOrders = refundedOrderIds.Length;
            summary.ActiveReturnRequests = latestReturnByOrderId.Count(entry =>
                ActiveReturnStatuses.Contains(entry.Value, StringComparer.OrdinalIgnoreCase));

            if (refundedOrderIds.Length > 0)
            {
                summary.RefundedRevenue = await _context.Orders
                    .AsNoTracking()
                    .Where(order => order.seller_id == sellerId && refundedOrderIds.Contains(order.OrderID))
                    .SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0m;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch analytics summary for seller {SellerId}. Returning defaults.", sellerId);
        }

        return summary;
    }

    public async Task<int[]> GetAvailableRevenueYearsAsync(
        int sellerId,
        CancellationToken cancellationToken = default)
    {
        if (sellerId <= 0 || string.IsNullOrWhiteSpace(_connectionString))
        {
            return Array.Empty<int>();
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var years = new List<int>();
            var query = $@"
                SELECT DISTINCT YEAR(OrderDate) AS RevenueYear
                FROM dbo.Orders
                WHERE seller_id = @SellerId
                    AND OrderDate IS NOT NULL
                    AND Status IN ({RevenueStatusesSql})
                ORDER BY RevenueYear DESC;
            ";

            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@SellerId", sellerId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader["RevenueYear"] is not DBNull)
                {
                    years.Add(Convert.ToInt32(reader["RevenueYear"]));
                }
            }

            return years.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch revenue years for seller {SellerId}. Returning empty results.", sellerId);
            return Array.Empty<int>();
        }
    }

    public async Task<Dictionary<int, List<decimal>>> GetMonthlyRevenueByYearAsync(
        int sellerId,
        int[] years,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, List<decimal>>();

        if (sellerId <= 0 || string.IsNullOrWhiteSpace(_connectionString) || years is null || years.Length == 0)
        {
            return result;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var year in years)
            {
                var monthlyRevenue = new List<decimal>(new decimal[12]);
                result[year] = monthlyRevenue;

                var query = @"
                    SELECT 
                        ISNULL(MONTH(OrderDate), 0) AS [Month],
                        ISNULL(SUM(CAST(TotalAmount AS DECIMAL(18,2))), 0) AS TotalAmount
                    FROM dbo.Orders
                    WHERE seller_id = @SellerId
                        AND YEAR(OrderDate) = @Year
                        AND Status IN (" + RevenueStatusesSql + @")
                    GROUP BY MONTH(OrderDate)
                    ORDER BY MONTH(OrderDate)
                ";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@SellerId", sellerId);
                command.Parameters.AddWithValue("@Year", year);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var month = reader["Month"] is DBNull ? 0 : Convert.ToInt32(reader["Month"]);
                    var amount = reader["TotalAmount"] is DBNull ? 0m : Convert.ToDecimal(reader["TotalAmount"]);

                    if (month >= 1 && month <= 12)
                    {
                        monthlyRevenue[month - 1] = amount;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch monthly revenue for seller {SellerId}. Returning empty results.", sellerId);
        }

        return result;
    }

    public async Task<Dictionary<int, List<int>>> GetMonthlyOrdersByYearAsync(
        int sellerId,
        int[] years,
        CancellationToken cancellationToken = default)
    {
        return await GetMonthlyIntMetricByYearAsync(
            sellerId,
            years,
            "COUNT(*)",
            "TotalOrders",
            cancellationToken);
    }

    public async Task<Dictionary<int, List<int>>> GetMonthlyUnitsByYearAsync(
        int sellerId,
        int[] years,
        CancellationToken cancellationToken = default)
    {
        return await GetMonthlyIntMetricByYearAsync(
            sellerId,
            years,
            "SUM(CAST(ISNULL(Quantity, 0) AS INT))",
            "TotalUnits",
            cancellationToken);
    }

    public async Task<List<TopSellingProduct>> GetTopPerformingProductsAsync(
        int sellerId,
        int topCount = 5,
        DateTime? from = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<TopSellingProduct>();
        if (sellerId <= 0 || string.IsNullOrWhiteSpace(_connectionString))
        {
            return result;
        }

        if (topCount <= 0)
        {
            topCount = 5;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            result = await GetTopProductsFromOrderItemsAsync(connection, sellerId, topCount, from, cancellationToken);
            if (result.Count == 0)
            {
                result = await GetTopProductsFromOrdersAsync(connection, sellerId, topCount, from, cancellationToken);
            }

            for (var i = 0; i < result.Count; i++)
            {
                result[i].Rank = i + 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch top products for seller {SellerId}. Returning empty results.", sellerId);
            return new List<TopSellingProduct>();
        }

        return result;
    }

    public async Task<List<CategoryPerformanceViewModel>> GetTopCategoriesAsync(
        int sellerId,
        int topCount = 5,
        DateTime? from = null,
        CancellationToken cancellationToken = default)
    {
        var result = new List<CategoryPerformanceViewModel>();

        if (sellerId <= 0 || string.IsNullOrWhiteSpace(_connectionString))
        {
            return result;
        }

        if (topCount <= 0)
        {
            topCount = 5;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var query = @"
                SELECT TOP (@TopCount)
                    COALESCE(NULLIF(LTRIM(RTRIM(p.Category)), ''), 'General') AS Category,
                    SUM(ISNULL(oi.Quantity, 1)) AS UnitsSold,
                    SUM(ISNULL(oi.Quantity, 1) * ISNULL(oi.UnitPrice, 0)) AS RevenueGenerated
                FROM dbo.Orders o
                INNER JOIN dbo.OrderItems oi ON oi.OrderID = o.OrderID AND oi.ProductID IS NOT NULL
                LEFT JOIN dbo.Products p ON p.ProductId = oi.ProductID
                WHERE o.seller_id = @SellerId
                    AND o.Status IN (" + RevenueStatusesSql + @")
                    AND (@From IS NULL OR o.OrderDate >= @From)
                GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(p.Category)), ''), 'General')
                ORDER BY RevenueGenerated DESC, UnitsSold DESC, Category ASC;
            ";

            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@SellerId", sellerId);
            command.Parameters.AddWithValue("@TopCount", topCount);
            command.Parameters.Add("@From", System.Data.SqlDbType.DateTime).Value = (object?)from ?? DBNull.Value;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new CategoryPerformanceViewModel
                {
                    Category = Convert.ToString(reader["Category"]) ?? "General",
                    UnitsSold = reader["UnitsSold"] is DBNull ? 0 : Convert.ToInt32(reader["UnitsSold"]),
                    RevenueGenerated = reader["RevenueGenerated"] is DBNull ? 0m : Convert.ToDecimal(reader["RevenueGenerated"])
                });
            }

            var totalRevenue = result.Sum(item => item.RevenueGenerated);
            if (totalRevenue > 0)
            {
                foreach (var category in result)
                {
                    category.RevenueShare = decimal.Round((category.RevenueGenerated / totalRevenue) * 100m, 2);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch top categories for seller {SellerId}. Returning empty results.", sellerId);
        }

        return result;
    }

    private static async Task<List<TopSellingProduct>> GetTopProductsFromOrderItemsAsync(
        SqlConnection connection,
        int sellerId,
        int topCount,
        DateTime? from,
        CancellationToken cancellationToken)
    {
        var query = @"
            SELECT TOP (@TopCount)
                COALESCE(NULLIF(LTRIM(RTRIM(p.ProductName)), ''), CONCAT('Product #', oi.ProductID)) AS ProductName,
                ISNULL(MIN(pv.SKU), '') AS Sku,
                COALESCE(NULLIF(LTRIM(RTRIM(p.Category)), ''), 'General') AS Category,
                COALESCE(MIN(NULLIF(LTRIM(RTRIM(p.ImagePath)), '')), '') AS ImageUrl,
                SUM(ISNULL(oi.Quantity, 1)) AS UnitsSold,
                SUM(ISNULL(oi.Quantity, 1) * ISNULL(oi.UnitPrice, 0)) AS RevenueGenerated
            FROM dbo.Orders o
            INNER JOIN dbo.OrderItems oi ON oi.OrderID = o.OrderID AND oi.ProductID IS NOT NULL
            LEFT JOIN dbo.Products p ON p.ProductId = oi.ProductID
            LEFT JOIN dbo.ProductVariants pv ON pv.ProductId = oi.ProductID AND pv.Size = oi.Size
            WHERE o.seller_id = @SellerId
                AND o.Status IN (" + RevenueStatusesSql + @")
                AND (@From IS NULL OR o.OrderDate >= @From)
            GROUP BY oi.ProductID, p.ProductName, p.Category
            ORDER BY UnitsSold DESC, RevenueGenerated DESC, ProductName ASC;
        ";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@SellerId", sellerId);
        command.Parameters.AddWithValue("@TopCount", topCount);
        command.Parameters.Add("@From", System.Data.SqlDbType.DateTime).Value = (object?)from ?? DBNull.Value;

        var products = new List<TopSellingProduct>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new TopSellingProduct
            {
                ProductName = Convert.ToString(reader["ProductName"]) ?? "(Unnamed Product)",
                Sku = Convert.ToString(reader["Sku"]) ?? "-",
                Category = Convert.ToString(reader["Category"]) ?? "General",
                ImageUrl = NormalizeImageUrl(Convert.ToString(reader["ImageUrl"])),
                UnitsSold = reader["UnitsSold"] is DBNull ? 0 : Convert.ToInt32(reader["UnitsSold"]),
                RevenueGenerated = reader["RevenueGenerated"] is DBNull ? 0m : Convert.ToDecimal(reader["RevenueGenerated"])
            });
        }

        return products;
    }

    private static async Task<List<TopSellingProduct>> GetTopProductsFromOrdersAsync(
        SqlConnection connection,
        int sellerId,
        int topCount,
        DateTime? from,
        CancellationToken cancellationToken)
    {
        var query = @"
            SELECT TOP (@TopCount)
                o.ProductName AS ProductName,
                '-' AS Sku,
                COALESCE((SELECT TOP 1 Category FROM dbo.Products WHERE ProductName = o.ProductName), 'General') AS Category,
                '' AS ImageUrl,
                SUM(ISNULL(o.Quantity, 1)) AS UnitsSold,
                SUM(ISNULL(o.TotalAmount, 0)) AS RevenueGenerated
            FROM dbo.Orders o
            WHERE o.seller_id = @SellerId
                AND o.Status IN (" + RevenueStatusesSql + @")
                AND o.ProductName IS NOT NULL
                AND LTRIM(RTRIM(o.ProductName)) <> ''
                AND (@From IS NULL OR o.OrderDate >= @From)
            GROUP BY o.ProductName
            ORDER BY UnitsSold DESC, RevenueGenerated DESC, ProductName ASC;
        ";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@SellerId", sellerId);
        command.Parameters.AddWithValue("@TopCount", topCount);
        command.Parameters.Add("@From", System.Data.SqlDbType.DateTime).Value = (object?)from ?? DBNull.Value;

        var products = new List<TopSellingProduct>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new TopSellingProduct
            {
                ProductName = Convert.ToString(reader["ProductName"]) ?? "(Unnamed Product)",
                Sku = Convert.ToString(reader["Sku"]) ?? "-",
                Category = Convert.ToString(reader["Category"]) ?? "General",
                ImageUrl = NormalizeImageUrl(Convert.ToString(reader["ImageUrl"])),
                UnitsSold = reader["UnitsSold"] is DBNull ? 0 : Convert.ToInt32(reader["UnitsSold"]),
                RevenueGenerated = reader["RevenueGenerated"] is DBNull ? 0m : Convert.ToDecimal(reader["RevenueGenerated"])
            });
        }

        return products;
    }

    private static string NormalizeImageUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Trim().Replace('\\', '/');
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (normalized.StartsWith("~/", StringComparison.Ordinal))
        {
            return normalized[1..];
        }

        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            return normalized;
        }

        return "/" + normalized;
    }

    private async Task<Dictionary<int, List<int>>> GetMonthlyIntMetricByYearAsync(
        int sellerId,
        int[] years,
        string aggregateExpression,
        string columnAlias,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, List<int>>();

        if (sellerId <= 0 || string.IsNullOrWhiteSpace(_connectionString) || years is null || years.Length == 0)
        {
            return result;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var year in years)
            {
                var monthlyValues = new List<int>(new int[12]);
                result[year] = monthlyValues;

                var query = $@"
                    SELECT
                        ISNULL(MONTH(OrderDate), 0) AS [Month],
                        ISNULL({aggregateExpression}, 0) AS {columnAlias}
                    FROM dbo.Orders
                    WHERE seller_id = @SellerId
                        AND YEAR(OrderDate) = @Year
                        AND Status IN ({RevenueStatusesSql})
                    GROUP BY MONTH(OrderDate)
                    ORDER BY MONTH(OrderDate);
                ";

                await using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@SellerId", sellerId);
                command.Parameters.AddWithValue("@Year", year);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var month = reader["Month"] is DBNull ? 0 : Convert.ToInt32(reader["Month"]);
                    var value = reader[columnAlias] is DBNull ? 0 : Convert.ToInt32(reader[columnAlias]);

                    if (month >= 1 && month <= 12)
                    {
                        monthlyValues[month - 1] = value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch monthly metric {ColumnAlias} for seller {SellerId}. Returning empty results.", columnAlias, sellerId);
        }

        return result;
    }
}

