using System.Collections.Generic;

namespace NextHorizon.Models
{
    public class PurchaseViewModel
    {
        public int ToPayCount { get; set; }
        public int ToShipCount { get; set; }
        public int ToReceiveCount { get; set; }
        public int ToReviewCount { get; set; }
        public int ReturnsCount { get; set; }
        public List<PurchaseHistory> PurchaseHistory { get; set; } = new();
        public UserInfo CurrentUser { get; set; } = new();
    }
}
