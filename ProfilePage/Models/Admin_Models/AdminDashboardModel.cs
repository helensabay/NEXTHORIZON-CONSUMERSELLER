using System;
using System.Collections.Generic;

namespace NextHorizon.Models.Admin_Models
{
    // The "Big Wrapper" that holds everything for the Dashboard page
    public class SuperAdminDashboardViewModel
    {
        public PlatformStats Stats { get; set; }
        public List<TopSellerViewModel> TopSellers { get; set; }
        public List<ConsumerLeaderboardViewModel> ConsumerLeaderboard { get; set; }
    }

    public class PlatformStats
    {
        public int TotalConsumers { get; set; }
        public int TotalSellers { get; set; }
        public int ActiveChallenges { get; set; }
        public string TotalKudos { get; set; } // String to handle "115.3K" formatting
    }

    public class TopSellerViewModel
    {
        public string ShopName { get; set; }
        public int MostPurchasedCount { get; set; } // Top 5 purchased items
        public int TotalProductsSold { get; set; }
        public string GrowthStatus { get; set; }
    }

    public class ConsumerLeaderboardViewModel
    {
        public int Rank { get; set; }
        public string UserName { get; set; }
        public decimal StravaKM { get; set; }
        public string Pace { get; set; }
        public bool IsVerified { get; set; }
    }

  
}
