using NextHorizon.Models.Admin_Models; // Make sure this matches your folder
using System;
using System.Collections.Generic;

namespace NextHorizon.Services.AdminServices
{
    public class DashboardService
    {
        public SuperAdminDashboardViewModel GetHeroStats()
        {
            return new SuperAdminDashboardViewModel
            {
                // Mapping to your "PlatformStats" class
                Stats = new PlatformStats
                {
                    TotalConsumers = 9120,
                    TotalSellers = 1450,
                    ActiveChallenges = 110,
                    TotalKudos = "115.3K"
                },

                // Mapping to your "TopSellerViewModel" class
                TopSellers = new List<TopSellerViewModel>
                {
                    new TopSellerViewModel { ShopName = "GearWorks Pro", MostPurchasedCount = 1036, TotalProductsSold = 3800, GrowthStatus = "High Growth" },
                    new TopSellerViewModel { ShopName = "Stride Athletics", MostPurchasedCount = 842, TotalProductsSold = 2150, GrowthStatus = "Active" },
                    new TopSellerViewModel { ShopName = "Monochrome Co.", MostPurchasedCount = 520, TotalProductsSold = 1100, GrowthStatus = "Active" },
                    new TopSellerViewModel { ShopName = "GeoChrome", MostPurchasedCount = 400, TotalProductsSold = 1100, GrowthStatus = "Active" },
                    new TopSellerViewModel { ShopName = "Aesthetics Co.", MostPurchasedCount = 380, TotalProductsSold = 1100, GrowthStatus = "Active" }
                },

                // Initialize empty or mock data for Leaderboard
                ConsumerLeaderboard = new List<ConsumerLeaderboardViewModel>
                {
                    new ConsumerLeaderboardViewModel { Rank = 1, UserName = "Leo V.", StravaKM = 14.6m, Pace = "5:20/KM", IsVerified = true },
                    new ConsumerLeaderboardViewModel { Rank = 2, UserName = "Matt M.", StravaKM = 13.6m, Pace = "4:20/KM", IsVerified = true },
                    new ConsumerLeaderboardViewModel { Rank = 3, UserName = "Miles O.", StravaKM = 12.6m, Pace = "3:20/KM", IsVerified = true },
                    new ConsumerLeaderboardViewModel { Rank = 4, UserName = "Nathan L.", StravaKM = 11.6m, Pace = "2:20/KM", IsVerified = true },
                    new ConsumerLeaderboardViewModel { Rank = 5, UserName = "Keith K.", StravaKM = 10.6m, Pace = "1:20/KM", IsVerified = true }
                }
            };
        }
    }
}