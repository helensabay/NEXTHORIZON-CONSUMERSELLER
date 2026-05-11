namespace MyAspNetApp.Models
{
    public class PromotionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Percentage Discount";
        public string BannerSize { get; set; } = "300x250";
        public decimal TotalDiscountPercent { get; set; }
        public decimal TotalDiscountFix { get; set; }
        public int UsageLimit { get; set; } = 100;
        public bool UntilPromotionLast { get; set; }
        public int BuyQuantity { get; set; } = 2;
        public int TakeQuantity { get; set; } = 1;
        public string FreeItemRequirement { get; set; } = "ExactProduct";
        public int ReturnWindowDays { get; set; } = 30;
        public List<string> ReturnReasons { get; set; } = new();
        public List<string> ReturnConditionRequirements { get; set; } = new();
        public string MinimumRequirementType { get; set; } = "None";
        public decimal MinimumPurchaseAmount { get; set; } = 100;
        public string Status { get; set; } = "Active";
        public List<string> SelectedProductIds { get; set; } = new();
    }
}
