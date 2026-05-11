namespace MyAspNetApp.Models
{
    public class VoucherViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VoucherType { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; } = DateTime.MinValue;
        public DateTime? RedeemedDate { get; set; }
        public bool IsUsed { get; set; }
        public bool IsExpired { get; set; }
    }
}
