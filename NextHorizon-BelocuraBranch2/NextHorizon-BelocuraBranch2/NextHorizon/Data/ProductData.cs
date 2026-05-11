using NextHorizon.Models;

namespace NextHorizon.Data;

public static class ProductData
{
    private const int StrideLabSellerId = 1;
    private const int PaceHausSellerId = 202;
    private const string PlaceholderProductImage = "/images/Storefront/placeholders/product-card.svg";
    private const string PlaceholderProductAltImage = "/images/Storefront/placeholders/product-detail.svg";
    private const string PlaceholderSellerAvatar = "/images/Storefront/placeholders/seller-avatar.svg";
    private const string PlaceholderSellerCover = "/images/Storefront/placeholders/seller-cover.svg";
    private const string PlaceholderReviewImage = "/images/Storefront/placeholders/review-photo.svg";

    public static List<Product> Products { get; } = BuildProducts();
    public static List<Seller> Sellers { get; } = BuildSellers();
    public static List<CartItem> Cart { get; } = new();
    public static List<WishlistItem> Wishlist { get; } = new();
    public static List<PurchaseRecord> PurchaseRecords { get; } = BuildPurchaseRecords();
    public static List<OrderConfirmationViewModel> Orders { get; } = new();

    public static string CreateOrderId()
        => $"NH-{DateTime.UtcNow:yyyyMMdd}-{Orders.Count + 1:D4}";

    public static bool HasSeller(int sellerId)
        => Sellers.Any(seller => seller.Id == sellerId);

    public static void SaveOrder(OrderConfirmationViewModel order)
    {
        Orders.Insert(0, order);

        foreach (var item in order.OrderItems)
        {
            var record = PurchaseRecords.FirstOrDefault(p => p.ProductId == item.ProductId);
            if (record is null)
            {
                PurchaseRecords.Add(new PurchaseRecord
                {
                    ProductId = item.ProductId,
                    HasPurchased = true,
                    IsDelivered = false,
                    DeliveryDate = order.EstimatedDeliveryDate,
                });
                continue;
            }

            record.HasPurchased = true;
            record.IsDelivered = false;
            record.DeliveryDate = order.EstimatedDeliveryDate;
        }
    }

    private static List<Seller> BuildSellers()
    {
        return
        [
            new Seller
            {
                Id = StrideLabSellerId,
                ShopName = "Stride Lab",
                Avatar = PlaceholderSellerAvatar,
                CoverImage = PlaceholderSellerCover,
                Location = "Makati City",
                ResponseTime = "within 15 min",
                ResponseRate = 98,
                TotalProducts = 3,
                TotalRatings = 1840,
                Followers = 12600,
                Rating = 4.8,
                JoinedAgo = "2 years ago",
                Description = "Performance-focused footwear and apparel for everyday training blocks and race day builds.",
            },
            new Seller
            {
                Id = PaceHausSellerId,
                ShopName = "Pace Haus",
                Avatar = PlaceholderSellerAvatar,
                CoverImage = PlaceholderSellerCover,
                Location = "Taguig City",
                ResponseTime = "within 30 min",
                ResponseRate = 95,
                TotalProducts = 3,
                TotalRatings = 1124,
                Followers = 8400,
                Rating = 4.7,
                JoinedAgo = "18 months ago",
                Description = "Curated race-ready kits, breathable layers, and recovery staples for high-mileage weeks.",
            },
        ];
    }

    private static List<Product> BuildProducts()
    {
        return
        [
            new Product
            {
                Id = 101,
                Name = "Apex Carbon Racer 2026",
                Brand = "Next Horizon",
                Category = "Men",
                SubCategory = "Running Shoes",
                Description = "A lightweight plated racer tuned for fast long runs and goal-race efforts.",
                Price = 12995m,
                OriginalPrice = 14995m,
                Image = PlaceholderProductImage,
                Images =
                [
                    PlaceholderProductImage,
                    PlaceholderProductAltImage,
                    PlaceholderProductAltImage,
                ],
                AvailableColors = ["Volt", "Crimson"],
                ColorImages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Volt"] =
                    [
                        PlaceholderProductImage,
                        PlaceholderProductAltImage,
                    ],
                    ["Crimson"] =
                    [
                        PlaceholderProductAltImage,
                        PlaceholderProductAltImage,
                    ],
                },
                Sizes = ["8", "8.5", "9", "9.5", "10", "10.5", "11"],
                Stock = 12,
                Rating = 4.9,
                ReviewCount = 3,
                SellerId = StrideLabSellerId,
                Reviews =
                [
                    new Review
                    {
                        Id = 1,
                        UserName = "Marco",
                        Rating = 5,
                        Comment = "Snappy underfoot and stable enough for long marathon sessions.",
                        Date = DateTime.UtcNow.AddDays(-14),
                        VerifiedPurchase = true,
                        Comfort = 5,
                        Quality = 5,
                        SizeFit = 3,
                        WidthFit = 3,
                        Images =
                        [
                            PlaceholderReviewImage,
                        ],
                    },
                    new Review
                    {
                        Id = 2,
                        UserName = "Iris",
                        Rating = 5,
                        Comment = "The turnover is excellent and the upper disappears once you start running.",
                        Date = DateTime.UtcNow.AddDays(-9),
                        VerifiedPurchase = true,
                        Comfort = 4,
                        Quality = 5,
                        SizeFit = 3,
                        WidthFit = 3,
                    },
                    new Review
                    {
                        Id = 3,
                        UserName = "Ken",
                        Rating = 4,
                        Comment = "Fast shoe, but I would not size down if you like more room in the forefoot.",
                        Date = DateTime.UtcNow.AddDays(-4),
                        VerifiedPurchase = true,
                        Comfort = 4,
                        Quality = 4,
                        SizeFit = 2,
                        WidthFit = 2,
                    },
                ],
            },
            new Product
            {
                Id = 102,
                Name = "Terra Tempo Half Zip",
                Brand = "Next Horizon",
                Category = "Men",
                SubCategory = "Outerwear",
                Description = "Technical half zip built for cool-weather tempo runs and recovery jogs.",
                Price = 3595m,
                Image = PlaceholderProductImage,
                Images =
                [
                    PlaceholderProductImage,
                    PlaceholderProductAltImage,
                ],
                AvailableColors = ["Navy", "Black"],
                ColorImages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Navy"] =
                    [
                        PlaceholderProductImage,
                    ],
                    ["Black"] =
                    [
                        PlaceholderProductAltImage,
                    ],
                },
                Sizes = ["S", "M", "L", "XL"],
                Stock = 20,
                Rating = 4.6,
                ReviewCount = 2,
                SellerId = StrideLabSellerId,
                Reviews =
                [
                    new Review
                    {
                        Id = 1,
                        UserName = "Luis",
                        Rating = 5,
                        Comment = "Breathes well and keeps chill off during early starts.",
                        Date = DateTime.UtcNow.AddDays(-18),
                        VerifiedPurchase = true,
                        Comfort = 4,
                        Quality = 5,
                    },
                    new Review
                    {
                        Id = 2,
                        UserName = "Paul",
                        Rating = 4,
                        Comment = "Athletic fit. Size up if you want room for a thicker base layer.",
                        Date = DateTime.UtcNow.AddDays(-7),
                        VerifiedPurchase = true,
                        Comfort = 4,
                        Quality = 4,
                        SizeFit = 2,
                    },
                ],
            },
            new Product
            {
                Id = 103,
                Name = "Cloudstride Bra",
                Brand = "Next Horizon",
                Category = "Women",
                SubCategory = "Sports Bra",
                Description = "Medium-support bra with soft compression for daily training and studio crossover.",
                Price = 2495m,
                Image = PlaceholderProductImage,
                Images =
                [
                    PlaceholderProductImage,
                    PlaceholderProductAltImage,
                ],
                AvailableColors = ["Rose", "Black"],
                ColorImages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Rose"] =
                    [
                        PlaceholderProductImage,
                    ],
                    ["Black"] =
                    [
                        PlaceholderProductAltImage,
                    ],
                },
                Sizes = ["XS", "S", "M", "L"],
                Stock = 16,
                Rating = 4.7,
                ReviewCount = 2,
                SellerId = PaceHausSellerId,
                Reviews =
                [
                    new Review
                    {
                        Id = 1,
                        UserName = "Bianca",
                        Rating = 5,
                        Comment = "Supportive without feeling restrictive during intervals.",
                        Date = DateTime.UtcNow.AddDays(-12),
                        VerifiedPurchase = true,
                        Comfort = 5,
                        Quality = 4,
                    },
                    new Review
                    {
                        Id = 2,
                        UserName = "Tina",
                        Rating = 4,
                        Comment = "Soft fabric and clean finish. Straps are slightly snug.",
                        Date = DateTime.UtcNow.AddDays(-3),
                        VerifiedPurchase = true,
                        Comfort = 4,
                        Quality = 4,
                        SizeFit = 2,
                    },
                ],
            },
            new Product
            {
                Id = 104,
                Name = "Pulse Flow Shorts",
                Brand = "Next Horizon",
                Category = "Women",
                SubCategory = "Bottoms",
                Description = "Split shorts with bounce-free rear storage and a smooth brief liner.",
                Price = 2195m,
                OriginalPrice = 2595m,
                Image = PlaceholderProductImage,
                Images =
                [
                    PlaceholderProductImage,
                    PlaceholderProductAltImage,
                ],
                AvailableColors = ["Black", "Light Blue"],
                ColorImages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Black"] =
                    [
                        PlaceholderProductImage,
                    ],
                    ["Light Blue"] =
                    [
                        PlaceholderProductAltImage,
                    ],
                },
                Sizes = ["XS", "S", "M", "L"],
                Stock = 0,
                IsRestockPreOrder = true,
                RestockDate = "March 20, 2026",
                RestockNote = "Reserve now and we will ship as soon as the next production batch lands.",
                Rating = 4.5,
                ReviewCount = 1,
                SellerId = PaceHausSellerId,
                Reviews =
                [
                    new Review
                    {
                        Id = 1,
                        UserName = "Aly",
                        Rating = 4,
                        Comment = "Great pocket layout and no ride-up on longer runs.",
                        Date = DateTime.UtcNow.AddDays(-21),
                        VerifiedPurchase = true,
                        Comfort = 4,
                        Quality = 4,
                    },
                ],
            },
            new Product
            {
                Id = 105,
                Name = "Summit Bottle Belt",
                Brand = "Next Horizon",
                Category = "Men",
                SubCategory = "Accessories",
                Description = "Minimal bounce waist belt with dual bottle holsters and phone sleeve.",
                Price = 1895m,
                Image = PlaceholderProductImage,
                Images =
                [
                    PlaceholderProductImage,
                ],
                AvailableColors = ["Black"],
                ColorImages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Black"] =
                    [
                        PlaceholderProductImage,
                    ],
                },
                Sizes = ["S", "M", "L"],
                Stock = 32,
                Rating = 4.4,
                ReviewCount = 0,
                SellerId = StrideLabSellerId,
            },
            new Product
            {
                Id = 106,
                Name = "Aurora Race Singlet",
                Brand = "Next Horizon",
                Category = "Women",
                SubCategory = "Tops",
                Description = "Featherweight singlet with laser-cut ventilation for humid race mornings.",
                Price = 2795m,
                Image = PlaceholderProductImage,
                Images =
                [
                    PlaceholderProductImage,
                ],
                AvailableColors = ["Rose", "White"],
                ColorImages = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Rose"] =
                    [
                        PlaceholderProductImage,
                    ],
                    ["White"] =
                    [
                        PlaceholderProductAltImage,
                    ],
                },
                Sizes = ["XS", "S", "M", "L"],
                Stock = 10,
                IsPreOrder = true,
                ExpectedReleaseDate = "March 15, 2026",
                PreOrderNote = "Limited first drop for members. Orders ship immediately after launch.",
                Rating = 4.8,
                ReviewCount = 0,
                SellerId = PaceHausSellerId,
            },
        ];
    }

    private static List<PurchaseRecord> BuildPurchaseRecords()
    {
        return
        [
            new PurchaseRecord
            {
                ProductId = 101,
                HasPurchased = true,
                IsDelivered = true,
                DeliveryDate = DateTime.UtcNow.AddDays(-2),
            },
            new PurchaseRecord
            {
                ProductId = 103,
                HasPurchased = true,
                IsDelivered = false,
                DeliveryDate = DateTime.UtcNow.AddDays(2),
            },
        ];
    }
}