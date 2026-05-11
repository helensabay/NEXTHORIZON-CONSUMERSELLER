namespace MyAspNetApp.Models.ViewModels
{
    public class ProductApprovalDashboardViewModel
    {
        public List<DbProduct> PendingProducts { get; set; } = new();
        public List<DbProduct> ApprovedProducts { get; set; } = new();
        public List<DbProduct> RejectedProducts { get; set; } = new();
    }
}
