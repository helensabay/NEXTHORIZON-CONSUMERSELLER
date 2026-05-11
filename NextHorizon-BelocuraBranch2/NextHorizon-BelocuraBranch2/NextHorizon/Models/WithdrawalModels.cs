// WithdrawalModels.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NextHorizon.Models
{
    public class WithdrawalRequestModel
    {
        [Required]
        [Range(100, double.MaxValue, ErrorMessage = "Minimum withdrawal amount is ₱100")]
        public decimal Amount { get; set; }

        [Required]
        public int PayoutAccountId { get; set; }
    }

    public class WithdrawalDetailsViewModel
    {
        public string SellerName { get; set; } = string.Empty;
        public decimal AvailableBalance { get; set; }
        public decimal PendingBalance { get; set; }
        public decimal TotalEarned { get; set; }
        public decimal TotalWithdrawn { get; set; }
        public List<PayoutAccountViewModel> PayoutAccounts { get; set; } = new();
        public List<RecentWithdrawalViewModel> RecentWithdrawals { get; set; } = new();
    }

    public class RecentWithdrawalViewModel
    {
        public long WithdrawalId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }

        // Helper property for formatted amount
        public string FormattedAmount => Amount.ToString("N2");
        
        // Helper property for formatted date
        public string FormattedDate => RequestedAt.ToString("MMM dd, yyyy hh:mm tt");
        
        // Helper property for status badge CSS class
        public string StatusBadgeClass
        {
            get
            {
                return Status?.ToLower() switch
                {
                    "pending" => "status-pending",
                    "approved" => "status-approved",
                    "completed" => "status-completed",
                    "rejected" => "status-rejected",
                    "cancelled" => "status-cancelled",
                    _ => "status-pending"
                };
            }
        }
    }

    public class WithdrawalHistoryViewModel
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public string? FilterStatus { get; set; }
        public List<WithdrawalHistoryItem> Withdrawals { get; set; } = new();
    }

    public class WithdrawalHistoryItem
    {
        public long WithdrawalId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        
        public string StatusBadgeClass
        {
            get
            {
                return Status?.ToLower() switch
                {
                    "pending" => "status-pending",
                    "approved" => "status-approved",
                    "completed" => "status-completed",
                    "rejected" => "status-rejected",
                    _ => "status-pending"
                };
            }
        }
        // Helper property for formatted amount
        public string FormattedAmount => Amount.ToString("N2");
        
        // Helper property for formatted request date
        public string FormattedRequestDate => RequestedAt.ToString("MMM dd, yyyy hh:mm tt");
        
        // Helper property for formatted processed date
        public string FormattedProcessedDate => ProcessedAt?.ToString("MMM dd, yyyy hh:mm tt") ?? "—";
    }
}
