namespace NextHorizon.Models;
    public class PromotionViewModel
    {
        public int Id { get; set; } // Used to know if we are Editing
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Percentage Discount";
        public string BannerSize { get; set; } = "300 x 250";
        public decimal? TotalDiscountPercent { get; set; }
        public decimal? TotalDiscountFix { get; set; }
        public int UsageLimit { get; set; } = 100;
        public bool UntilPromotionLast { get; set; }
        public int BuyQuantity { get; set; } = 2;
        public int TakeQuantity { get; set; } = 1;
        public string FreeItemRequirement { get; set; } = "ExactProduct";
        public int ReturnWindowDays { get; set; } = 30;
        public List<string> ReturnReasons { get; set; } = new List<string>();
        public List<string> ReturnConditionRequirements { get; set; } = new List<string>();
        public string MinimumRequirementType { get; set; } = "None";
        public decimal MinimumPurchaseAmount { get; set; } = 100;
        public string Status { get; set; } = "Pending";
        public DateTime? StartDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public DateTime? EndDate { get; set; }
        public TimeSpan? EndTime { get; set; }
        public List<int> SelectedProductIds { get; set; } = new List<int>();
    }
