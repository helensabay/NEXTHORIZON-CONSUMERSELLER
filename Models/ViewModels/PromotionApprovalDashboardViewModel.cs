namespace MyAspNetApp.Models.ViewModels
{
    public class PromotionApprovalDashboardViewModel
    {
        public List<PromotionViewModel> PendingPromotions { get; set; } = new();
        public List<PromotionViewModel> ApprovedPromotions { get; set; } = new();
        public List<PromotionViewModel> RejectedPromotions { get; set; } = new();
    }
}
