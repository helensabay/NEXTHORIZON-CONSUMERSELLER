using MyAspNetApp.Models;

namespace MyAspNetApp.Models.ViewModels
{
    public class SellerRatingsViewModel
    {
        public DbProduct Product { get; set; } = new DbProduct();
        public List<SellerRatingItem> Reviews { get; set; } = new();
    }

    public class SellerRatingItem
    {
        public DbReview Review { get; set; } = new DbReview();
        public List<string> Images { get; set; } = new();
    }
}
