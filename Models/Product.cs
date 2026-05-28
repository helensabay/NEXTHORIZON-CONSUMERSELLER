using System.ComponentModel.DataAnnotations;

namespace MyAspNetApp.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }  // set when product is on sale
    public string Image { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new(); // Multiple product images
    public List<string> Sizes { get; set; } = new();
    public string Gender { get; set; } = "Unisex"; // "Men", "Women", or "Unisex"

    // optional SKU and physical attributes
    // product-level SKU/dimensions are deprecated; prefer variant values when available
    public string SKU { get; set; } = string.Empty;
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Height { get; set; }
    public decimal? Width { get; set; }
    public string Category { get; set; } = string.Empty; // e.g. "Tops", "Accessories"
    public string SubCategory { get; set; } = string.Empty; // "Running Shoes", "Apparel", etc.
    public string Brand { get; set; } = string.Empty; // "Nike", "Adidas", etc.
    public List<string> AvailableColors { get; set; } = new(); // Color options
    public Dictionary<string, List<string>> ColorImages { get; set; } = new(); // color name → image gallery
    public Dictionary<string, int> ColorStocks { get; set; } = new(); // color name → stock quantity
    public Dictionary<string, List<string>> ColorSizes { get; set; } = new(); // color name → available sizes

    // list of variants carrying per-variant attributes (price, SKU, dimensions, stock, etc.)
    public List<ProductVariant> Variants { get; set; } = new();

    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public List<Review> Reviews { get; set; } = new();
    public int Stock { get; set; } = 50;
    public bool IsPreOrder { get; set; } = false;
    public string ExpectedReleaseDate { get; set; } = string.Empty; // e.g. "March 2026"
    public string PreOrderNote { get; set; } = string.Empty; // e.g. "Ships in 4–6 weeks"
    // Existing product sold out but available for pre-order restock
    public bool IsRestockPreOrder { get; set; } = false;
    public string RestockDate { get; set; } = string.Empty; // e.g. "Mid-March 2026"
    public string RestockNote { get; set; } = string.Empty; // e.g. "Limited restock — reserve yours now"
    public int SellerId { get; set; } = 1; // FK to seller
}

public class ProductVariant
{
    public int Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Availability { get; set; } = "In Stock";
    public string ImagePath { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Height { get; set; }
    public decimal? Width { get; set; }
}

public class Seller
{
    public int Id { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty; // URL or path to seller avatar
    public string CoverImage { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double Rating { get; set; }
    public int TotalRatings { get; set; }
    public int TotalProducts { get; set; }
    public int Followers { get; set; }
    public bool IsFollowing { get; set; }
    public double ResponseRate { get; set; } // percentage
    public string ResponseTime { get; set; } = string.Empty; // e.g. "within hours"
    public DateTime JoinedDate { get; set; }
    public string JoinedAgo => GetTimeAgo(JoinedDate);

    private static string GetTimeAgo(DateTime date)
    {
        var span = DateTime.Now - date;
        if (span.TotalDays >= 365) return $"{(int)(span.TotalDays / 365)} year{((int)(span.TotalDays / 365) != 1 ? "s" : "")} ago";
        if (span.TotalDays >= 30) return $"{(int)(span.TotalDays / 30)} month{((int)(span.TotalDays / 30) != 1 ? "s" : "")} ago";
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays} day{((int)span.TotalDays != 1 ? "s" : "")} ago";
        return "today";
    }
}

public class Review
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public double Rating { get; set; }
    public string ShortReview { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool VerifiedPurchase { get; set; } = true;
    public string Email { get; set; } = string.Empty;
    public bool Recommend { get; set; }
    public int ProductId { get; set; }
    public List<string> Images { get; set; } = new(); // Optional customer photos
    // Metric ratings
    public int? Comfort { get; set; }    // 1–5: 1=Uncomfortable, 5=Comfortable
    public int? Quality { get; set; }    // 1–5: 1=Poor, 5=Perfect
    public int? SizeFit { get; set; }    // 1=Too small, 2=Slightly small, 3=Perfect, 4=Slightly big, 5=Too big
    public int? WidthFit { get; set; }   // 1=Too narrow, 2=Slightly narrow, 3=Perfect, 4=Slightly wide, 5=Too wide
    public string? SellerReply { get; set; }
    public DateTime? SellerReplyDate { get; set; }
}

public class CartItem
{
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int SellerId { get; set; }
    public decimal UnitPrice { get; set; }
}

public class WishlistItem
{
    public int ProductId { get; set; }
    public DateTime AddedDate { get; set; }
}

public class PurchaseRecord
{
    public int ProductId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public bool IsDelivered => DeliveryDate.HasValue;
}

public class CheckoutItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public int SellerId { get; set; }
    public string Size { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class CheckoutViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Postal code is required.")]
    public string PostalCode { get; set; } = string.Empty;

    public string DeliveryOption { get; set; } = "Standard";

    public string PaymentMethod { get; set; } = "Card";
    public string? CardNumber { get; set; }
    public string? CardExpiry { get; set; }
    public string? CardCvv { get; set; }
    public int? SelectedVoucherId { get; set; }
    public string AppliedVoucherName { get; set; } = string.Empty;
    public decimal VoucherDiscount { get; set; }
    public List<CheckoutVoucherOption> AvailableVouchers { get; set; } = new();
    public List<CheckoutAddressOption> SavedAddresses { get; set; } = new();

    public List<CheckoutItem> CartItems { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total => Math.Max(0m, Subtotal + ShippingFee - VoucherDiscount);
}

public class CheckoutVoucherOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
}

public class CheckoutAddressOption
{
    public string Label { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class OrderConfirmationViewModel
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = "Pending";
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string DeliveryOption { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "Card";
    public DateTime CreatedAt { get; set; }
    public DateTime EstimatedDeliveryDate { get; set; }
    public List<CheckoutItem> OrderItems { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public string AppliedVoucherName { get; set; } = string.Empty;
    public decimal VoucherDiscount { get; set; }
    public decimal Total => Math.Max(0m, Subtotal + ShippingFee - VoucherDiscount);

    public string NormalizedStatus => NormalizeStatus(OrderStatus);

    public string StatusColor => NormalizedStatus switch
    {
        "Pending" => "#f59e0b",
        "Processing" => "#3b82f6",
        "Shipped" => "#8b5cf6",
        "Delivered" => "#22c55e",
        "Cancelled" => "#ef4444",
        _ => "#888"
    };

    public int CurrentStep => NormalizedStatus switch
    {
        "Pending" => 0,
        "Processing" => 1,
        "Shipped" => 2,
        "Delivered" => 3,
        _ => 0
    };

    private static string NormalizeStatus(string? status)
    {
        return (status ?? string.Empty).Trim() switch
        {
            "Placed" => "Pending",
            var value => value
        };
    }
}
