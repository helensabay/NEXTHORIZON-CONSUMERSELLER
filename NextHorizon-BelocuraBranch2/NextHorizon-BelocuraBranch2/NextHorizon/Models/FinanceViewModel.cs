using System;
using System.Collections.Generic;

namespace NextHorizon.Models
{
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
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
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
        public string AdditionalDetails { get; set; } = string.Empty;
    }
}
