using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextHorizon.Models
{
    public class SellerDashboardViewModel
    {
        public string SellerName { get; set; } = string.Empty;
        public DateTime CurrentDate { get; set; }
        public int ReturnRequests { get; set; }
        public int PendingOrders { get; set; }
        public int LowStockAlerts { get; set; }
        public decimal WithdrawAmount { get; set; }
        public string WithdrawStatus { get; set; } = string.Empty;
        public decimal TodayOrderValue { get; set; }
        public decimal YesterdayOrderValue { get; set; }
        public decimal TodaySales { get; set; }
        public int TodayOrderCount { get; set; }
        public int TodayUnitsSold { get; set; }
        public int ShippedTodayCount { get; set; }
        public int InFulfillmentCount { get; set; }
        public int OpenOrderCount { get; set; }
        public int TotalUnitsSold { get; set; }
        public int TotalOrders { get; set; }
        public decimal SalesGrowth { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal RecognizedRevenueToday { get; set; }
        public decimal YesterdayRecognizedRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal RefundedRevenue { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal PipelineRevenue { get; set; }
        public int CancelledOrders { get; set; }
        public int RefundedOrders { get; set; }
        public int ActiveReturnRequests { get; set; }
        public decimal CodDeliveredRevenue { get; set; }
        public decimal CodExposure { get; set; }
        public decimal CodSuccessRate { get; set; }
        public int CodRtsCount { get; set; }
        public int TotalVisits { get; set; }
        public List<Order> RecentOrders { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
        public List<TopSellingProduct> TopProducts { get; set; } = new();
        public Dictionary<string, List<TopSellingProduct>> TopProductsByRange { get; set; } = new();
        public Dictionary<int, List<decimal>> MonthlyRevenueByYear { get; set; } = new();
        public Dictionary<int, List<int>> MonthlyOrdersByYear { get; set; } = new();
        public Dictionary<int, List<int>> MonthlyUnitsByYear { get; set; } = new();
        public List<CategoryPerformanceViewModel> TopCategories { get; set; } = new();
        public List<SellerDashboardNotificationViewModel> Notifications { get; set; } = new();
        public int UnreadNotificationCount => Notifications.Count(notification => !notification.IsRead);
    }

    public class SellerDashboardNotificationViewModel
    {
        public string RecipientType { get; set; } = "seller";
        public string RecipientId { get; set; } = string.Empty;
        public int? OrderId { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Priority { get; set; } = "medium";
        public bool ActionRequired { get; set; }
        public string DeliveryMode { get; set; } = "in_app";
        public string? LinkType { get; set; }
        public string? LinkTarget { get; set; }
    }

    public class Order
    {
        [Key]
        public int OrderID { get; set; }
        public string? FullName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string? Status { get; set; } = string.Empty;
        public string? ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public int seller_id { get; set; }
        [Column("ConsumerID")]
        public int? ConsumerID { get; set; }
        public int? logistics_id { get; set; }

        [NotMapped]
        public decimal Amount { get; set; }
        [NotMapped]
        public string Sku { get; set; } = string.Empty;
        [NotMapped]
        public string Size { get; set; } = string.Empty;
        [NotMapped]
        public string ProductImage { get; set; } = string.Empty;
        public string? PaymentMethod { get; set; } = string.Empty;
        [NotMapped]
        public string Courier { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? DeliveryOption { get; set; }
        [NotMapped]
        public string EffectiveStatus { get; set; } = string.Empty;
        public string? TrackingNumber { get; set; }
        public string? ReturnReason { get; set; }
        public string? ReturnNote { get; set; }
        public string? SellerNote { get; set; }
        [NotMapped]
        public string ReturnProofImage { get; set; } = string.Empty;
        [NotMapped]
        public decimal CalculatedTotal => Subtotal + ShippingFee;
        [NotMapped]
        public List<OrderItem> OrderItems { get; set; } = new();
    }

    public class OrderItem
    {
        public int OrderItemId { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string ProductImage { get; set; } = string.Empty;
        public ProductSummary? Product { get; set; }
    }

    public class ProductSummary
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Category { get; set; }
    }

    public partial class ReturnRequest
    {
        public int ReturnId { get; set; }
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public int SellerId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? SellerDecisionReason { get; set; }
        public string? SellerDecisionNote { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string BuyerName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class Logistics
    {
        public int logistics_id { get; set; }
        public string courier_name { get; set; } = string.Empty;
    }

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

    public sealed class CategoryPerformanceViewModel
    {
        public string Category { get; set; } = string.Empty;
        public int UnitsSold { get; set; }
        public decimal RevenueGenerated { get; set; }
        public decimal RevenueShare { get; set; }
    }

    public class FinanceViewModel
    {
        public string SellerName { get; set; } = string.Empty;
        public DateTime CurrentDate { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal PendingBalance { get; set; }
        public decimal TotalEarned { get; set; }
        public decimal TotalWithdrawn { get; set; }
        public decimal TodayRevenue { get; set; }
        public decimal ThisMonthRevenue { get; set; }
        public int PendingPayoutCount { get; set; }
        public decimal TotalPendingWithdrawal { get; set; }
        public List<FinanceTransactionViewModel> Transactions { get; set; } = new();
    }

    public class FinanceTransactionViewModel
    {
        public string ReferenceId { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TransactionHistoryViewModel
    {
        public string SellerName { get; set; } = string.Empty;
        public DateTime CurrentDate { get; set; }
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
        public List<FinanceTransactionViewModel> Transactions { get; set; } = new();

        public string? FilterType { get; set; }
        public string? FilterStatus { get; set; }
        public DateTime? FilterFromDate { get; set; }
        public DateTime? FilterToDate { get; set; }
    }

    public class BalanceDetailsViewModel
    {
        public string SellerName { get; set; } = string.Empty;
        public DateTime CurrentDate { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal PendingBalance { get; set; }
        public decimal TotalEarned { get; set; }
        public decimal TotalWithdrawn { get; set; }
        public int PendingPayoutCount { get; set; }
        public decimal TotalPendingWithdrawal { get; set; }
        public List<FinanceTransactionViewModel> RecentTransactions { get; set; } = new();
    }

    public class TransactionDetailDto
    {
        public string ReferenceId { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public Dictionary<string, string?> AdditionalDetails { get; set; } = new();
    }
}
