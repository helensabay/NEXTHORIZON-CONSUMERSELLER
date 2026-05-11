using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextHorizon.Models;

public sealed class Product
{
    [Column("ProductId")]
    public int Id { get; set; }

    [Column("ProductName")]
    public string Name { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    [NotMapped] // ✨ Doesn't exist in SQL yet
    public string? SubCategory { get; set; }

    [Column("Details")] // ✨ SQL uses 'Details', not 'Description'
    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    [NotMapped] // ✨ Doesn't exist in SQL yet
    public decimal? OriginalPrice { get; set; }

    [Column("ImagePath")] // ✨ SQL uses 'ImagePath', not 'Image'
    public string Image { get; set; } = string.Empty;

    [NotMapped]
    public List<string> Images { get; set; } = new();
    [NotMapped]
    public List<string> Sizes { get; set; } = new();
    [NotMapped]
    public List<string> AvailableColors { get; set; } = new();
    [NotMapped]
    public Dictionary<string, List<string>> ColorImages { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public int Stock { get; set; }

    // ✨ NONE OF THESE EXIST IN SQL YET, SO WE MUST IGNORE THEM ALL! ✨
    [NotMapped] public bool IsPreOrder { get; set; }
    [NotMapped] public bool IsRestockPreOrder { get; set; }
    [NotMapped] public string? ExpectedReleaseDate { get; set; }
    [NotMapped] public string? PreOrderNote { get; set; }
    [NotMapped] public string? RestockDate { get; set; }
    [NotMapped] public string? RestockNote { get; set; }
    [NotMapped] public double Rating { get; set; }
    [NotMapped] public int ReviewCount { get; set; }
    
    [NotMapped]
    public List<Review> Reviews { get; set; } = new();

    [Column("seller_id")]
    public int SellerId { get; set; }
}

public sealed class Seller
{
    public int Id { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string CoverImage { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResponseTime { get; set; } = string.Empty;
    public int ResponseRate { get; set; }
    public int TotalProducts { get; set; }
    public int TotalRatings { get; set; }
    public int Followers { get; set; }
    public double Rating { get; set; }
    public string JoinedAgo { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class Review
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool VerifiedPurchase { get; set; }
    public List<string> Images { get; set; } = new();
    public int? Comfort { get; set; }
    public int? Quality { get; set; }
    public int? SizeFit { get; set; }
    public int? WidthFit { get; set; }
}

public sealed class CartItem
{
    public int ProductId { get; set; }
    public string Size { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public sealed class WishlistItem
{
    public int ProductId { get; set; }
}

public sealed class PurchaseRecord
{
    public int ProductId { get; set; }
    public bool HasPurchased { get; set; }
    public bool IsDelivered { get; set; }
    public DateTime? DeliveryDate { get; set; }
}

public sealed class CheckoutCartItemViewModel
{
    public int ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Image { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Price { get; init; }
}

public sealed class CheckoutViewModel
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string PostalCode { get; set; } = string.Empty;

    public string DeliveryOption { get; set; } = "Standard";
    public string PaymentMethod { get; set; } = "Card";
    public List<CheckoutCartItemViewModel> CartItems { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
}

public sealed class OrderConfirmationViewModel
{
    public string OrderId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string Address { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string DeliveryOption { get; init; } = "Standard";
    public string PaymentMethod { get; init; } = "Card";
    public string OrderStatus { get; init; } = "Placed";
    public string StatusColor { get; init; } = "#0a0a0a";
    public int CurrentStep { get; init; }
    public List<CheckoutCartItemViewModel> OrderItems { get; init; } = new();
    public decimal Subtotal { get; init; }
    public decimal ShippingFee { get; init; }
    public decimal Total { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime EstimatedDeliveryDate { get; init; } = DateTime.UtcNow.AddDays(5);
}
