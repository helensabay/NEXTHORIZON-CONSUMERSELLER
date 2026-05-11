using MyAspNetApp.Models;

namespace MyAspNetApp.Data;

public static class ProductData
{
    // ── Sellers ──
    public static List<Seller> Sellers { get; } = new()
    {
        new Seller
        {
            Id = 1,
            ShopName = "RunVault Official",
            Avatar = "https://ui-avatars.com/api/?name=RunVault&background=1a1a2e&color=fff&size=128&bold=true",
            CoverImage = "/images/cover-photo/Skinny_1920x420__35_.jpg",
            Description = "Your #1 destination for premium running shoes. We carry authentic products from the world's top brands. Fast shipping and hassle-free returns!",
            Location = "Manila, Philippines",
            Rating = 4.9,
            TotalRatings = 12540,
            TotalProducts = 86,
            Followers = 34200,
            ResponseRate = 98,
            ResponseTime = "within hours",
            JoinedDate = DateTime.Now.AddYears(-5)
        },
        new Seller
        {
            Id = 2,
            ShopName = "SportZone PH",
            Avatar = "https://ui-avatars.com/api/?name=SportZone&background=0f3460&color=fff&size=128&bold=true",
            CoverImage = "/images/cover-photo/Skinny_1920x420__35_.jpg",
            Description = "Premium athletic wear & footwear. Authorized dealer of Nike, Adidas, and more. Serving Filipino athletes since 2019.",
            Location = "Cebu City, Philippines",
            Rating = 4.7,
            TotalRatings = 8320,
            TotalProducts = 124,
            Followers = 21500,
            ResponseRate = 95,
            ResponseTime = "within minutes",
            JoinedDate = DateTime.Now.AddYears(-3)
        },
        new Seller
        {
            Id = 3,
            ShopName = "Kicks & Beyond",
            Avatar = "https://ui-avatars.com/api/?name=Kicks+Beyond&background=533483&color=fff&size=128&bold=true",
            CoverImage = "/images/cover-photo/Skinny_1920x420__35_.jpg",
            Description = "Curated sneaker boutique featuring the latest drops and exclusive colorways. Authenticity guaranteed on every pair.",
            Location = "Davao City, Philippines",
            Rating = 4.8,
            TotalRatings = 5670,
            TotalProducts = 52,
            Followers = 15800,
            ResponseRate = 92,
            ResponseTime = "within a day",
            JoinedDate = DateTime.Now.AddMonths(-18)
        }
    };

    public static List<Product> Products { get; } = new()
    {
        // Men's Running Products
        new Product
        {
            Id = 1,
            Name = "Horizon Elite Road Runner",
            SKU = "HR-ELITE-001",
            Weight = 1.2m,
            Length = 30m,
            Height = 15m,
            Width = 10m,
            Description = "Premium carbon-plated running shoes engineered for speed and endurance. Features advanced cushioning technology and breathable mesh upper for maximum performance.",
            Price = 8999.00m,
            Image = "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers.avif",
            SellerId = 1,
            Images = new List<string> {
                "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers.avif",
                "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (1).avif",
                "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (2).avif",
                "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (3).avif",
                "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (4).avif",
                "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (5).avif",
                "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (6).avif"
            },
            Sizes = new List<string> { "US 7", "US 8", "US 9", "US 10", "US 11", "US 12" },
            Category = "Men",
            SubCategory = "Running Shoes",
            Brand = "Nike",
            AvailableColors = new List<string> { "Lime Shimmer-Green Lux", "Green Lux-Lime Shimmer" },
            ColorImages = new Dictionary<string, List<string>> {
                ["Lime Shimmer-Green Lux"] = new List<string> {
                    "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers.avif",
                    "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (1).avif",
                    "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (2).avif",
                    "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (3).avif",
                    "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (4).avif",
                    "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (5).avif",
                    "/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (6).avif"
                },
                ["Green Lux-Lime Shimmer"] = new List<string> {
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (7).avif",
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (8).avif",
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (9).avif",
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (10).avif",
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (11).avif",
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (12).avif"
                }
            },
            Rating = 4.8,
            ReviewCount = 245,
            Reviews = new List<Review>
            {
                new Review { Id = 1, UserName = "Carlos M.", Rating = 5, Comment = "Best running shoes for marathon training! Worth every peso. The carbon plate gives incredible propulsion — used these for my last 42K and felt zero fatigue.", Date = DateTime.Now.AddDays(-5), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2,
                    Images = new List<string> {
                        "https://images.pexels.com/photos/2404843/pexels-photo-2404843.jpeg?auto=compress&cs=tinysrgb&w=400",
                        "https://images.pexels.com/photos/2529148/pexels-photo-2529148.jpeg?auto=compress&cs=tinysrgb&w=400"
                    }
                },
                new Review { Id = 2, UserName = "Rafael S.", Rating = 5, Comment = "Lightweight and responsive. Perfect for long runs. Took them straight out of the box to a 20K without any break-in issues.", Date = DateTime.Now.AddDays(-10), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2,
                    Images = new List<string> {
                        "https://images.pexels.com/photos/1598508/pexels-photo-1598508.jpeg?auto=compress&cs=tinysrgb&w=400"
                    }
                },
                new Review { Id = 3, UserName = "Jose P.", Rating = 5, Comment = "Excellent cushioning! My knees feel great after long runs.", Date = DateTime.Now.AddDays(-15), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2 },
                new Review { Id = 4, UserName = "Mark T.", Rating = 4, Comment = "Great shoes, slight break-in period but worth it.", Date = DateTime.Now.AddDays(-20), Comfort = 4, Quality = 4, SizeFit = 2, WidthFit = 2,
                    Images = new List<string> {
                        "https://images.pexels.com/photos/1456706/pexels-photo-1456706.jpeg?auto=compress&cs=tinysrgb&w=400",
                        "https://images.pexels.com/photos/2385477/pexels-photo-2385477.jpeg?auto=compress&cs=tinysrgb&w=400",
                        "https://images.pexels.com/photos/1464625/pexels-photo-1464625.jpeg?auto=compress&cs=tinysrgb&w=400"
                    }
                },
                new Review { Id = 5, UserName = "David L.", Rating = 5, Comment = "Perfect for road running. Very durable and comfortable.", Date = DateTime.Now.AddDays(-25), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2 },
                new Review { Id = 6, UserName = "Rico G.", Rating = 5, Comment = "These shoes helped me achieve my personal best in a half marathon!", Date = DateTime.Now.AddDays(-30), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2 },
                new Review { Id = 7, UserName = "Anthony V.", Rating = 4, Comment = "Good quality, runs true to size. Highly recommend.", Date = DateTime.Now.AddDays(-35), Comfort = 4, Quality = 4, SizeFit = 2, WidthFit = 2 }
            }
        },
        new Product
        {
            Id = 2,
            Name = "Sprint Pro Training Shoes",
            SKU = "SP-TRAIN-002",
            Weight = 1.1m,
            Length = 29m,
            Height = 14m,
            Width = 9.5m,
            Description = "Versatile training shoes with responsive cushioning perfect for speed work and interval training. Durable outsole for all-terrain running.",
            Price = 6499.00m,
            Image = "https://images.pexels.com/photos/1598505/pexels-photo-1598505.jpeg?auto=compress&cs=tinysrgb&w=600",
            Images = new List<string> {
                "https://images.pexels.com/photos/1598505/pexels-photo-1598505.jpeg?auto=compress&cs=tinysrgb&w=600",
                "https://images.pexels.com/photos/2529147/pexels-photo-2529147.jpeg?auto=compress&cs=tinysrgb&w=600",
                "https://images.pexels.com/photos/1464625/pexels-photo-1464625.jpeg?auto=compress&cs=tinysrgb&w=600",
                "https://images.pexels.com/photos/1102776/pexels-photo-1102776.jpeg?auto=compress&cs=tinysrgb&w=600"
            },
            Sizes = new List<string> { "US 7", "US 8", "US 9", "US 10", "US 11", "US 12" },
            Category = "Men",
            SubCategory = "Running Shoes",
            Brand = "Adidas",
            SellerId = 2,
            AvailableColors = new List<string> { "White", "Grey", "Black" },
            Rating = 4.6,
            ReviewCount = 198,
            Reviews = new List<Review>
            {
                new Review { Id = 3, UserName = "Miguel P.", Rating = 5, Comment = "Great for tempo runs and track workouts!", Date = DateTime.Now.AddDays(-3), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2 }
            }
        },
        new Product
        {
            Id = 3,
            Name = "Men's Performance Running Shorts",
            Description = "Lightweight 2-in-1 running shorts with moisture-wicking fabric and secure phone pocket. Built-in compression liner for support.",
            Price = 2499.00m,
            Image = "https://images.pexels.com/photos/5067731/pexels-photo-5067731.jpeg?auto=compress&cs=tinysrgb&w=600",
            Sizes = new List<string> { "S", "M", "L", "XL", "XXL" },
            Category = "Men",
            SubCategory = "Apparel",
            Brand = "Nike",
            SellerId = 1,
            AvailableColors = new List<string> { "Black", "Navy", "Charcoal" },
            Rating = 4.7,
            ReviewCount = 156,
            Reviews = new List<Review>()
        },
        new Product
        {
            Id = 4,
            Name = "Trail Runner XC",
            Description = "Rugged trail running shoes with aggressive grip and protective rock plate. Waterproof upper for all-weather adventures.",
            Price = 7499.00m,
            Image = "https://images.pexels.com/photos/1456705/pexels-photo-1456705.jpeg?auto=compress&cs=tinysrgb&w=600",
            Sizes = new List<string> { "US 7", "US 8", "US 9", "US 10", "US 11", "US 12" },
            Category = "Men",
            SubCategory = "Running Shoes",
            Brand = "Adidas",
            SellerId = 2,
            AvailableColors = new List<string> { "Brown", "Green", "Black" },
            Rating = 4.9,
            ReviewCount = 203,
            Stock = 0,
            IsPreOrder = true,
            ExpectedReleaseDate = "April 2026",
            PreOrderNote = "Ships in 4–6 weeks. Free shipping on all pre-orders.",
            Reviews = new List<Review>()
        },
        new Product
        {
            Id = 5,
            Name = "Men's Tech Running Singlet",
            Description = "Ultra-lightweight running singlet with seamless construction and reflective details for visibility during early morning or evening runs.",
            Price = 1899.00m,
            Image = "https://images.pexels.com/photos/8612988/pexels-photo-8612988.jpeg?auto=compress&cs=tinysrgb&w=600",
            Sizes = new List<string> { "S", "M", "L", "XL", "XXL" },
            Category = "Men",
            SubCategory = "Apparel",
            Brand = "Nike",
            SellerId = 2,
            AvailableColors = new List<string> { "White", "Black", "Red" },
            Rating = 4.5,
            ReviewCount = 134,
            Reviews = new List<Review>()
        },
        
        // Women's Running Products
        new Product
        {
            Id = 6,
            Name = "Horizon Elite Women's Runner",
            Description = "Precision-engineered women's running shoes with adaptive fit technology. Provides exceptional energy return and plush cushioning for distance running.",
            Price = 8799.00m,
            Image = "https://images.pexels.com/photos/2529147/pexels-photo-2529147.jpeg?auto=compress&cs=tinysrgb&w=600",
            SellerId = 1,
            Sizes = new List<string> { "US 5", "US 6", "US 7", "US 8", "US 9", "US 10" },
            Category = "Women",
            SubCategory = "Running Shoes",
            Brand = "Nike",
            AvailableColors = new List<string> { "Pink", "White", "Purple" },
            Rating = 4.9,
            ReviewCount = 287,
            Reviews = new List<Review>
            {
                new Review { Id = 4, UserName = "Maria C.", Rating = 5, Comment = "Perfect for my marathon training! Super comfortable even on 30km runs. These are hands-down the best shoes I've ever run in.", Date = DateTime.Now.AddDays(-2), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2,
                    Images = new List<string> {
                        "https://images.pexels.com/photos/2529147/pexels-photo-2529147.jpeg?auto=compress&cs=tinysrgb&w=400",
                        "https://images.pexels.com/photos/3621186/pexels-photo-3621186.jpeg?auto=compress&cs=tinysrgb&w=400"
                    }
                },
                new Review { Id = 5, UserName = "Anna S.", Rating = 5, Comment = "Love the fit and cushioning. Best investment for serious runners.", Date = DateTime.Now.AddDays(-7), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2,
                    Images = new List<string> {
                        "https://images.pexels.com/photos/2385477/pexels-photo-2385477.jpeg?auto=compress&cs=tinysrgb&w=400"
                    }
                },
                new Review { Id = 6, UserName = "Gina R.", Rating = 5, Comment = "Amazing support and stability. My go-to running shoes now!", Date = DateTime.Now.AddDays(-12), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2 },
                new Review { Id = 7, UserName = "Patricia L.", Rating = 4, Comment = "Very comfortable, great for long distance. Slight sizing issue but overall excellent.", Date = DateTime.Now.AddDays(-18), Comfort = 4, Quality = 4, SizeFit = 1, WidthFit = 2 },
                new Review { Id = 8, UserName = "Michelle A.", Rating = 5, Comment = "These shoes are a game changer! No more foot fatigue.", Date = DateTime.Now.AddDays(-22), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2,
                    Images = new List<string> {
                        "https://images.pexels.com/photos/5067731/pexels-photo-5067731.jpeg?auto=compress&cs=tinysrgb&w=400",
                        "https://images.pexels.com/photos/4056535/pexels-photo-4056535.jpeg?auto=compress&cs=tinysrgb&w=400"
                    }
                },
                new Review { Id = 9, UserName = "Sarah D.", Rating = 5, Comment = "Worth every peso! Best running shoes I've ever owned.", Date = DateTime.Now.AddDays(-28), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2 }
            }
        },
        new Product
        {
            Id = 7,
            Name = "Women's Velocity Racer",
            Description = "Lightweight racing flats designed for speed. Minimalist design with responsive foam for PR attempts and race day performance.",
            Price = 5999.00m,
            Image = "https://images.pexels.com/photos/2385477/pexels-photo-2385477.jpeg?auto=compress&cs=tinysrgb&w=600",
            Sizes = new List<string> { "US 5", "US 6", "US 7", "US 8", "US 9", "US 10" },
            Category = "Women",
            SubCategory = "Running Shoes",
            Brand = "Adidas",
            SellerId = 3,
            AvailableColors = new List<string> { "Yellow", "Black", "White" },
            Rating = 4.7,
            ReviewCount = 167,
            Reviews = new List<Review>
            {
                new Review { Id = 6, UserName = "Sofia R.", Rating = 5, Comment = "Shaved 2 minutes off my 10K time! Highly recommend.", Date = DateTime.Now.AddDays(-1), Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2 }
            }
        },
        new Product
        {
            Id = 8,
            Name = "Women's High-Impact Running Bra",
            Description = "Maximum support sports bra engineered for runners. Moisture-wicking fabric with adjustable straps and breathable mesh panels.",
            Price = 2899.00m,
            Image = "https://images.pexels.com/photos/6454243/pexels-photo-6454243.jpeg?auto=compress&cs=tinysrgb&w=600",
            Sizes = new List<string> { "XS", "S", "M", "L", "XL" },
            Category = "Women",
            SubCategory = "Apparel",
            Brand = "Nike",
            AvailableColors = new List<string> { "Black", "Grey", "Navy" },
            Rating = 4.8,
            ReviewCount = 234,
            Reviews = new List<Review>()
        },
        new Product
        {
            Id = 9,
            Name = "Women's Running Tights",
            Description = "Premium compression running tights with reflective elements. Features secure zip pocket and flatlock seams to prevent chafing.",
            Price = 3299.00m,
            Image = "https://images.pexels.com/photos/4056535/pexels-photo-4056535.jpeg?auto=compress&cs=tinysrgb&w=600",
            Sizes = new List<string> { "XS", "S", "M", "L", "XL" },
            Category = "Women",
            SubCategory = "Apparel",
            Brand = "Adidas",
            SellerId = 2,
            AvailableColors = new List<string> { "Black", "Purple", "Teal" },
            Rating = 4.6,
            ReviewCount = 192,
            Reviews = new List<Review>()
        },
        new Product
        {
            Id = 10,
            Name = "Women's Distance Runner Tank",
            Description = "Breathable running tank with mesh back panel. Lightweight, quick-drying fabric keeps you cool during long runs.",
            Price = 1699.00m,
            Image = "https://images.pexels.com/photos/5069432/pexels-photo-5069432.jpeg?auto=compress&cs=tinysrgb&w=600",
            Sizes = new List<string> { "XS", "S", "M", "L", "XL" },
            Category = "Women",
            SubCategory = "Apparel",
            Brand = "Nike",
            AvailableColors = new List<string> { "White", "Pink", "Light Blue" },
            Rating = 4.5,
            ReviewCount = 145,
            Reviews = new List<Review>()
        },

        // ── SOLD OUT — RESTOCK PRE-ORDER ─────────────────────────────────────
        new Product
        {
            Id = 12,
            Name = "Horizon Elite Road Runner v2",
            Description = "The beloved Horizon Elite returns with an upgraded carbon-fibre plate and a wider toe box based on customer feedback. Same race-proven DNA, refined for 2026. This edition sold out within 48 hours of launch — reserve yours before the restock ships.",
            Price = 9499.00m,
            Image = "https://images.pexels.com/photos/2529148/pexels-photo-2529148.jpeg?auto=compress&cs=tinysrgb&w=600",
            Images = new List<string> {
                "https://images.pexels.com/photos/2529148/pexels-photo-2529148.jpeg?auto=compress&cs=tinysrgb&w=600",
                "https://images.pexels.com/photos/1598508/pexels-photo-1598508.jpeg?auto=compress&cs=tinysrgb&w=600",
                "https://images.pexels.com/photos/1456706/pexels-photo-1456706.jpeg?auto=compress&cs=tinysrgb&w=600"
            },
            Sizes = new List<string> { "US 7", "US 8", "US 9", "US 10", "US 11", "US 12" },
            Category = "Men",
            SubCategory = "Running Shoes",
            Brand = "Nike",
            AvailableColors = new List<string> { "Black/White", "Volt/Black", "Navy/Silver" },
            Rating = 4.9,
            ReviewCount = 312,
            Stock = 0,
            IsPreOrder = false,
            IsRestockPreOrder = true,
            RestockDate = "Mid-March 2026",
            RestockNote = "Limited restock — only 200 pairs available. Reserve yours now and pay at shipment.",
            Reviews = new List<Review>
            {
                new Review { Id = 1, UserName = "Carlo R.", Rating = 5, Comment = "This shoe is an absolute beast on race day. Sold out so fast — glad I got a pair first time around.", Date = DateTime.Now.AddDays(-14), VerifiedPurchase = true, Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2,
                    Images = new List<string> {
                        "https://images.pexels.com/photos/2404843/pexels-photo-2404843.jpeg?auto=compress&cs=tinysrgb&w=400",
                        "https://images.pexels.com/photos/2529148/pexels-photo-2529148.jpeg?auto=compress&cs=tinysrgb&w=400"
                    }
                },
                new Review { Id = 2, UserName = "James M.", Rating = 5, Comment = "PR'd my half marathon in these. Need a second pair for training!", Date = DateTime.Now.AddDays(-21), VerifiedPurchase = true, Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2,
                    Images = new List<string> {
                        "https://images.pexels.com/photos/1598508/pexels-photo-1598508.jpeg?auto=compress&cs=tinysrgb&w=400"
                    }
                },
                new Review { Id = 3, UserName = "Allan T.", Rating = 5, Comment = "Incredibly responsive. The carbon plate really does make a difference.", Date = DateTime.Now.AddDays(-30), VerifiedPurchase = true, Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2 },
                new Review { Id = 4, UserName = "Ben V.", Rating = 4, Comment = "Runs slightly narrow — go half a size up. Otherwise perfect.", Date = DateTime.Now.AddDays(-45), VerifiedPurchase = true, Comfort = 4, Quality = 4, SizeFit = 2, WidthFit = 1 },
                new Review { Id = 5, UserName = "Mark S.", Rating = 5, Comment = "Worth every peso. My go-to for race day and tempo runs.", Date = DateTime.Now.AddDays(-60), VerifiedPurchase = true, Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2 }
            }
        },

        // ── UPCOMING / PRE-ORDER ──────────────────────────────────────────────
        new Product
        {
            Id = 11,
            Name = "Apex Carbon Racer 2026",
            Description = "The next generation race day shoe. Engineered with a full-length carbon fibre plate and next-gen nitrogen-infused foam for explosive energy return. Designed for sub-3 hour marathon runners who demand the absolute best.",
            Price = 12999.00m,
            Image = "https://images.pexels.com/photos/1598508/pexels-photo-1598508.jpeg?auto=compress&cs=tinysrgb&w=600",
            Images = new List<string> {
                "https://images.pexels.com/photos/1598508/pexels-photo-1598508.jpeg?auto=compress&cs=tinysrgb&w=600",
                "https://images.pexels.com/photos/2529148/pexels-photo-2529148.jpeg?auto=compress&cs=tinysrgb&w=600",
                "https://images.pexels.com/photos/1456706/pexels-photo-1456706.jpeg?auto=compress&cs=tinysrgb&w=600"
            },
            Sizes = new List<string> { "US 7", "US 8", "US 9", "US 10", "US 11", "US 12" },
            Category = "Men",
            SubCategory = "Running Shoes",
            Brand = "Nike",
            AvailableColors = new List<string> { "Black/Gold", "White/Carbon", "Crimson" },
            Rating = 0,
            ReviewCount = 0,
            Stock = 0,
            IsPreOrder = true,
            ExpectedReleaseDate = "March 2026",
            PreOrderNote = "Limited first-run batch. Ships March 15, 2026. Free priority shipping.",
            Reviews = new List<Review>()
        },

        // ── SALE PRODUCT ─────────────────────────────────────────────────────
        new Product
        {
            Id = 13,
            Name = "Aero Sprint V2",
            Description = "High-performance daily trainer with responsive foam midsole and breathable engineered knit upper. Versatile enough for easy runs, tempo sessions, and everything in between. Now on sale while stocks last.",
            Price = 3599.00m,
            OriginalPrice = 5999.00m,
            Image = "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (10).avif",
            Images = new List<string> {
                "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (10).avif",
                "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (11).avif",
                "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (12).avif",
                "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (9).avif",
                "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (8).avif",
                "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (7).avif"
            },
            Sizes = new List<string> { "US 7", "US 8", "US 9", "US 10", "US 11" },
            Category = "Men",
            SubCategory = "Running Shoes",
            Brand = "Adidas",
            SellerId = 3,
            AvailableColors = new List<string> { "Green Lux-Lime Shimmer" },
            ColorImages = new Dictionary<string, List<string>> {
                ["Green Lux-Lime Shimmer"] = new List<string> {
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (10).avif",
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (11).avif",
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (12).avif",
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (9).avif",
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (8).avif",
                    "/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (7).avif"
                }
            },
            Rating = 4.6,
            ReviewCount = 89,
            Stock = 14,
            Reviews = new List<Review>
            {
                new Review { Id = 1, UserName = "Paolo R.", Rating = 5, Comment = "Incredible value at this price — feels like a premium shoe.", Date = DateTime.Now.AddDays(-4), VerifiedPurchase = true, Comfort = 5, Quality = 4, SizeFit = 2, WidthFit = 2 },
                new Review { Id = 2, UserName = "Marco D.", Rating = 5, Comment = "Grabbed these on sale and I'm blown away. Super comfortable all day.", Date = DateTime.Now.AddDays(-9), VerifiedPurchase = true, Comfort = 5, Quality = 5, SizeFit = 2, WidthFit = 2 },
                new Review { Id = 3, UserName = "Lena V.", Rating = 4, Comment = "Great daily trainer, runs a tiny bit narrow but otherwise excellent.", Date = DateTime.Now.AddDays(-14), VerifiedPurchase = true, Comfort = 4, Quality = 4, SizeFit = 2, WidthFit = 1 }
            }
        }
    };
    
    private static readonly object CartSync = new();
    public static List<CartItem> Cart { get; } = new();
    public static List<WishlistItem> Wishlist { get; } = new();

    public static List<CartItem> GetCartSnapshot()
    {
        lock (CartSync)
        {
            return Cart
                .Select(CloneCartItem)
                .ToList();
        }
    }

    public static T WithCartLock<T>(Func<List<CartItem>, T> action)
    {
        lock (CartSync)
        {
            return action(Cart);
        }
    }

    public static void WithCartLock(Action<List<CartItem>> action)
    {
        lock (CartSync)
        {
            action(Cart);
        }
    }

    public static void ReplaceCart(IEnumerable<CartItem> items)
    {
        lock (CartSync)
        {
            Cart.Clear();
            Cart.AddRange(items.Select(CloneCartItem));
        }
    }

    private static CartItem CloneCartItem(CartItem item)
    {
        return new CartItem
        {
            ProductId = item.ProductId,
            VariantId = item.VariantId,
            Color = item.Color,
            Size = item.Size,
            Quantity = item.Quantity,
            SellerId = item.SellerId,
            UnitPrice = item.UnitPrice
        };
    }

    // Persistent order store (in-memory, cleared on restart)
    private static int _orderSeq = 3;
    public static int NextOrderSeq() => ++_orderSeq;

    public static List<MyAspNetApp.Models.OrderConfirmationViewModel> Orders { get; } = new()
    {
        new MyAspNetApp.Models.OrderConfirmationViewModel
        {
            OrderId      = "ORD-20260215-0001",
            OrderStatus  = "Delivered",
            FullName     = "Juan Dela Cruz",
            Email        = "juan@nexthorizon.ph",
            Phone        = "+63 912 345 6789",
            Address      = "123 Rizal St",
            City         = "Manila",
            PostalCode   = "1000",
            DeliveryOption   = "Standard",
            PaymentMethod    = "Card",
            CreatedAt        = DateTime.Now.AddDays(-15),
            EstimatedDeliveryDate = DateTime.Now.AddDays(-10),
            Subtotal   = 13335m,
            ShippingFee = 150m,
            OrderItems = new List<MyAspNetApp.Models.CheckoutItem>
            {
                new MyAspNetApp.Models.CheckoutItem { ProductId=1, SellerId=1, Name="Horizon Elite Road Runner", Image="/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers.avif", Size="US 9", Price=8999m, Quantity=1 },
                new MyAspNetApp.Models.CheckoutItem { ProductId=2, SellerId=1, Name="Velocity Pro Trainer", Image="/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (10).avif", Size="US 10", Price=4336m, Quantity=1 }
            }
        },
        new MyAspNetApp.Models.OrderConfirmationViewModel
        {
            OrderId      = "ORD-20260225-0002",
            OrderStatus  = "Shipped",
            FullName     = "Juan Dela Cruz",
            Email        = "juan@nexthorizon.ph",
            Phone        = "+63 912 345 6789",
            Address      = "123 Rizal St",
            City         = "Manila",
            PostalCode   = "1000",
            DeliveryOption   = "Express",
            PaymentMethod    = "PayPal",
            CreatedAt        = DateTime.Now.AddDays(-5),
            EstimatedDeliveryDate = DateTime.Now.AddDays(1),
            Subtotal   = 7849m,
            ShippingFee = 300m,
            OrderItems = new List<MyAspNetApp.Models.CheckoutItem>
            {
                new MyAspNetApp.Models.CheckoutItem { ProductId=3, SellerId=2, Name="Apex Carbon Racer", Image="/images/Lime Shimmer-Green Lux/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers.avif", Size="US 8", Price=7849m, Quantity=1 }
            }
        },
        new MyAspNetApp.Models.OrderConfirmationViewModel
        {
            OrderId      = "ORD-20260301-0003",
            OrderStatus  = "Processing",
            FullName     = "Juan Dela Cruz",
            Email        = "juan@nexthorizon.ph",
            Phone        = "+63 912 345 6789",
            Address      = "123 Rizal St",
            City         = "Manila",
            PostalCode   = "1000",
            DeliveryOption   = "Standard",
            PaymentMethod    = "COD",
            CreatedAt        = DateTime.Now.AddDays(-1),
            EstimatedDeliveryDate = DateTime.Now.AddDays(4),
            Subtotal   = 5845m,
            ShippingFee = 150m,
            OrderItems = new List<MyAspNetApp.Models.CheckoutItem>
            {
                new MyAspNetApp.Models.CheckoutItem { ProductId=2, SellerId=1, Name="Velocity Pro Trainer", Image="/images/Green Lux-Lime Shimmer/PUMA-x-ASTON-MARTIN-ARAMCO-F1®-TEAM-Fade-Men's-Sneakers (10).avif", Size="US 11", Price=5845m, Quantity=1 }
            }
        }
    };
    
    // Simulated purchase records - products that customer has received
    public static List<PurchaseRecord> PurchaseRecords { get; } = new()
    {
        new PurchaseRecord 
        { 
            ProductId = 1, 
            PurchaseDate = DateTime.Now.AddDays(-30),
            DeliveryDate = DateTime.Now.AddDays(-20)
        },
        new PurchaseRecord 
        { 
            ProductId = 6, 
            PurchaseDate = DateTime.Now.AddDays(-25),
            DeliveryDate = DateTime.Now.AddDays(-15)
        },
        new PurchaseRecord 
        { 
            ProductId = 3, 
            PurchaseDate = DateTime.Now.AddDays(-10),
            DeliveryDate = DateTime.Now.AddDays(-3)
        }
    };
}
