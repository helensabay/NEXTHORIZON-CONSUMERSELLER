using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyAspNetApp.Data;
using MyAspNetApp.Models;
using MyAspNetApp.Security;
using MyAspNetApp.Services;

namespace MyAspNetApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheDuration;
        private readonly ILogger<ProductsController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly MediaPathService _mediaPathService;

        public ProductsController(AppDbContext db, IMemoryCache cache, IConfiguration config, ILogger<ProductsController> logger, IWebHostEnvironment env, MediaPathService mediaPathService)
        {
            _db = db;
            _cache = cache;
            _logger = logger;
            _env = env;
            _mediaPathService = mediaPathService;
            _cacheDuration = TimeSpan.FromMinutes(config.GetValue<int>("Cache:ProductCacheMinutes", 5));
        }

        private async Task<List<DbProductColorImage>> LoadProductColorImagesAsync(List<int> productIds)
        {
            if (productIds.Count == 0)
            {
                return new List<DbProductColorImage>();
            }

            try
            {
                return await _db.ProductColorImages
                    .AsNoTracking()
                    .Where(ci => productIds.Contains(ci.ProductId))
                    .ToListAsync();
            }
            catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("ProductColorImages", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "ProductColorImages table was not found. Continuing without color image data.");
                return new List<DbProductColorImage>();
            }
        }

        private async Task<List<DbProductColorImage>> LoadProductColorImagesAsync(int productId)
        {
            try
            {
                return await _db.ProductColorImages
                    .AsNoTracking()
                    .Where(ci => ci.ProductId == productId)
                    .ToListAsync();
            }
            catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("ProductColorImages", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "ProductColorImages table was not found. Continuing without color image data for product {ProductId}.", productId);
                return new List<DbProductColorImage>();
            }
        }

        private static Review MapDbReview(DbReview review, List<DbReviewImage> reviewImages)
        {
            return new Review
            {
                Id = review.Id,
                UserName = review.UserName,
                Rating = (double)review.Rating,
                ShortReview = review.ShortReview,
                Comment = review.Comment,
                Date = review.Date,
                VerifiedPurchase = review.VerifiedPurchase,
                Email = review.Email,
                Recommend = review.Recommend,
                Comfort = review.Comfort,
                Quality = review.Quality,
                SizeFit = review.SizeFit,
                WidthFit = review.WidthFit,
                SellerReply = review.SellerReply,
                SellerReplyDate = review.SellerReplyDate,
                ProductId = review.ProductId,
                Images = reviewImages
                    .Where(i => i.ReviewId == review.Id)
                    .Select(i => i.ImageUrl)
                    .ToList()
            };
        }

        private Product MapDbProduct(
            DbProduct p,
            List<DbProductColorImage>? colorImgs = null,
            List<DbProductVariant>? variants = null,
            Dictionary<int, (double Rating, int Count)>? reviewStats = null,
            Dictionary<int, List<Review>>? reviewsByProduct = null)
        {
            var myColorImgs = colorImgs?.Where(ci => ci.ProductId == p.ProductId).ToList()
                              ?? new List<DbProductColorImage>();

            var productVariants = (variants ?? new List<DbProductVariant>())
                .Where(v => v.ProductId == p.ProductId)
                .ToList();

            var colorImageDict = productVariants
                .Where(v => !string.IsNullOrWhiteSpace(v.ColorName))
                .GroupBy(v => v.ColorName)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .Select(BuildVariantImageUrl)
                        .Where(url => !string.IsNullOrWhiteSpace(url))
                        .Distinct()
                        .ToList());

            foreach (var imageGroup in myColorImgs.GroupBy(ci => ci.ColorName))
            {
                if (!colorImageDict.TryGetValue(imageGroup.Key, out var urls) || urls.Count == 0)
                {
                    colorImageDict[imageGroup.Key] = imageGroup
                        .Select(ci => NormalizeImage(ci.ImagePath))
                        .Where(url => !string.IsNullOrWhiteSpace(url))
                        .Distinct()
                        .ToList();
                }
            }

            var variantsByColor = productVariants
                .GroupBy(v => v.ColorName)
                .ToDictionary(g => g.Key, g => g.ToList());

            var availColors = variantsByColor.Keys.ToList();
            if (availColors.Count == 0)
                availColors = colorImageDict.Keys.ToList();

            var colorStockDict = new Dictionary<string, int>();
            var colorSizesDict = new Dictionary<string, List<string>>();

            foreach (var color in availColors)
            {
                if (variantsByColor.TryGetValue(color, out var vlist))
                {
                    colorStockDict[color] = vlist.Sum(v => v.Stock);
                    var sizes = vlist
                        .SelectMany(v => (v.Sizes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct()
                        .ToList();
                    if (sizes.Any())
                        colorSizesDict[color] = sizes;
                }
                else
                {
                    colorStockDict[color] = p.Stock;
                }
            }

            var allSizes = productVariants
                .SelectMany(v => (v.Sizes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            var rating = 0d;
            var reviewCount = 0;
            if (reviewStats != null && reviewStats.TryGetValue(p.ProductId, out var stats))
            {
                rating = stats.Rating;
                reviewCount = stats.Count;
            }

            var mappedReviews = new List<Review>();
            if (reviewsByProduct != null && reviewsByProduct.TryGetValue(p.ProductId, out var fullReviews))
                mappedReviews = fullReviews;

            // Prefer variant-level SKU/dimensions (normalized schema) but fall back to product-level if present.
            var firstVariant = productVariants.FirstOrDefault();

            var sku = !string.IsNullOrWhiteSpace(p.SKU) ? p.SKU : firstVariant?.SKU ?? string.Empty;
            var weight = p.Weight ?? firstVariant?.Weight;
            var length = p.Length ?? firstVariant?.Length;
            var height = p.Height ?? firstVariant?.Height;
            var width = p.Width ?? firstVariant?.Width;

            // assemble image path: prefer first variant's image, then product fallback
            string imagePath = BuildVariantImageUrl(firstVariant);
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                imagePath = NormalizeImage(firstVariant?.ImagePath ?? p.ImagePath ?? string.Empty);
            }

            // compute price using variant first if available
            // compute price entirely from variant. if none supplied, default to 0.
            decimal basePrice = 0m;
            decimal? baseOriginal = null;
            if (firstVariant != null && firstVariant.Price.HasValue)
            {
                basePrice = firstVariant.Price.Value;
            }
            // else leave zero (caller may interpret as not priced)

            // convert db variants to API variant models
            var variantModels = productVariants.Select(v => new ProductVariant
            {
                Id = v.Id,
                SKU = v.SKU,
                Size = v.Size,
                Style = v.Style,
                Quantity = v.Quantity,
                Availability = v.Availability,
                ImagePath = BuildVariantImageUrl(v),
                Price = v.Price,
                Weight = v.Weight,
                Length = v.Length,
                Height = v.Height,
                Width = v.Width
            }).ToList();

            return new Product
            {
                Variants = variantModels,
                Id = p.ProductId,
                SellerId = p.SellerId > 0 ? p.SellerId : 1,
                Name = p.ProductName,
                Price = basePrice,
                OriginalPrice = baseOriginal,
                Image = imagePath,
                SKU = sku,
                Gender = p.Gender ?? "Unisex",
                Category = p.Category ?? "",
                // the database currently doesn't have a separate subcategory column;
                // we will leave this blank to avoid showing the same tag twice
                // (gender is now displayed separately on the UI).
                // TODO: add SubCategory column when needed and map appropriately.
                SubCategory = "",
                Brand = p.Brand ?? "",
                Weight = weight,
                Length = length,
                Height = height,
                Width = width,
                Description = p.Details ?? "",
                Rating = rating,
                ReviewCount = reviewCount,
                Reviews = mappedReviews,
                // compute total stock from variants since product.Stock no longer used
                Stock = productVariants.Sum(v => v.Quantity),
                Sizes = allSizes,
                AvailableColors = availColors,
                ColorImages = colorImageDict,
                ColorStocks = colorStockDict,
                ColorSizes = colorSizesDict
            };
        }

        private string NormalizeImage(string? path)
        {
            return _mediaPathService.NormalizePublicPath(path);
        }

        private string BuildVariantImageUrl(DbProductVariant? variant)
        {
            if (variant == null)
            {
                return string.Empty;
            }

            if (variant.ImageData != null && variant.ImageData.Length > 0)
            {
                return Url.Action(nameof(GetVariantImage), "Products", new
                {
                    variantId = variant.Id,
                    v = variant.ImageData.Length
                }) ?? $"/api/products/variant-image/{variant.Id}";
            }

            return NormalizeImage(variant.ImagePath);
        }

        [HttpGet("variant-image/{variantId:int}")]
        public async Task<IActionResult> GetVariantImage(int variantId)
        {
            try
            {
                var variant = await _db.ProductVariants
                    .AsNoTracking()
                    .Where(v => v.Id == variantId)
                    .Select(v => new
                    {
                        v.ImageData,
                        v.ImageMimeType,
                        v.ImagePath
                    })
                    .FirstOrDefaultAsync();

                if (variant?.ImageData != null && variant.ImageData.Length > 0)
                {
                    Response.Headers.CacheControl = "public,max-age=3600";
                    return File(
                        variant.ImageData,
                        string.IsNullOrWhiteSpace(variant.ImageMimeType) ? "application/octet-stream" : variant.ImageMimeType);
                }

                var fallbackPath = NormalizeImage(variant?.ImagePath);
                if (!string.IsNullOrWhiteSpace(fallbackPath))
                {
                    return Redirect(fallbackPath);
                }

                return NotFound();
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Database is temporarily unavailable." });
            }
        }

        // GET: api/products
        [HttpGet]
        public async Task<IActionResult> GetAllProducts()
        {
            try
            {
                var result = await _cache.GetOrCreateAsync("products:all", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

                    var dbProducts = await _db.Products
                        .AsNoTracking()
                        .Where(p => p.Status == "active" || p.Status == "approved")
                        .ToListAsync();

                    var productIds = dbProducts.Select(p => p.ProductId).ToList();
                    var colorImgs = await LoadProductColorImagesAsync(productIds);
                    var dbReviews = await _db.Reviews
                        .AsNoTracking()
                        .Where(r => productIds.Contains(r.ProductId))
                        .ToListAsync();
                    var variants = await _db.ProductVariants
                        .AsNoTracking()
                        .Where(v => productIds.Contains(v.ProductId))
                        .ToListAsync();

                    var reviewStats = dbReviews
                        .GroupBy(r => r.ProductId)
                        .ToDictionary(g => g.Key, g => ((double)g.Average(x => x.Rating), g.Count()));

                    return dbProducts.Select(p => MapDbProduct(p, colorImgs, variants, reviewStats)).ToList();
                });

                if (result == null)
                {
                    return Ok(new List<Product>());
                }

                return Ok(result);
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex))
            {
                _logger.LogError(ex, "Error fetching all products from the database.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Database is temporarily unavailable.",
                    detail = ex.Message
                });
            }
        }

        // GET: api/products/men
        [HttpGet("men")]
        public async Task<IActionResult> GetMenProducts()
        {
            try
            {
                var result = await _cache.GetOrCreateAsync("products:men", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

                    var products = await _db.Products
                        .AsNoTracking()
                        .Where(p => (p.Status == "active" || p.Status == "approved") && p.Gender == "Men")
                        .ToListAsync();
                    var productIds = products.Select(p => p.ProductId).ToList();
                    var colorImgs = await LoadProductColorImagesAsync(productIds);
                    var dbReviews = await _db.Reviews
                        .AsNoTracking()
                        .Where(r => productIds.Contains(r.ProductId))
                        .ToListAsync();

                    var variants = await _db.ProductVariants
                        .AsNoTracking()
                        .Where(v => productIds.Contains(v.ProductId))
                        .ToListAsync();

                    var reviewStats = dbReviews
                        .GroupBy(r => r.ProductId)
                        .ToDictionary(g => g.Key, g => ((double)g.Average(x => x.Rating), g.Count()));
                    return products.Select(p => MapDbProduct(p, colorImgs, variants, reviewStats)).ToList();
                });

                if (result == null)
                {
                    return Ok(new List<Product>());
                }

                return Ok(result);
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Database is temporarily unavailable." });
            }
        }

        // GET: api/products/women
        [HttpGet("women")]
        public async Task<IActionResult> GetWomenProducts()
        {
            try
            {
                var result = await _cache.GetOrCreateAsync("products:women", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = _cacheDuration;

                    var products = await _db.Products
                        .AsNoTracking()
                        .Where(p => (p.Status == "active" || p.Status == "approved") && p.Gender == "Women")
                        .ToListAsync();
                    var productIds = products.Select(p => p.ProductId).ToList();
                    var colorImgs = await LoadProductColorImagesAsync(productIds);
                    var dbReviews = await _db.Reviews
                        .AsNoTracking()
                        .Where(r => productIds.Contains(r.ProductId))
                        .ToListAsync();

                    var variants = await _db.ProductVariants
                        .AsNoTracking()
                        .Where(v => productIds.Contains(v.ProductId))
                        .ToListAsync();

                    var reviewStats = dbReviews
                        .GroupBy(r => r.ProductId)
                        .ToDictionary(g => g.Key, g => ((double)g.Average(x => x.Rating), g.Count()));
                    return products.Select(p => MapDbProduct(p, colorImgs, variants, reviewStats)).ToList();
                });

                if (result == null)
                {
                    return Ok(new List<Product>());
                }

                return Ok(result);
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Database is temporarily unavailable." });
            }
        }

        // GET: api/products/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProductById(int id)
        {
            try
            {
                var dbProduct = await _db.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProductId == id && (p.Status == "active" || p.Status == "approved"));
                if (dbProduct != null)
                {
                    var colorImgs = await LoadProductColorImagesAsync(id);
                    var dbReviews = await _db.Reviews
                        .AsNoTracking()
                        .Where(r => r.ProductId == id)
                        .OrderByDescending(r => r.Date)
                        .ToListAsync();
                    var reviewImages = await _db.ReviewImages
                        .AsNoTracking()
                        .Where(i => dbReviews.Select(r => r.Id).Contains(i.ReviewId))
                        .ToListAsync();

                    var mappedReviews = dbReviews
                        .GroupBy(r => r.ProductId)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(r => MapDbReview(r, reviewImages)).ToList());
                    var variants = await _db.ProductVariants
                        .AsNoTracking()
                        .Where(v => v.ProductId == id)
                        .ToListAsync();

                    var reviewStats = dbReviews
                        .GroupBy(r => r.ProductId)
                        .ToDictionary(g => g.Key, g => ((double)g.Average(x => x.Rating), g.Count()));

                    return Ok(MapDbProduct(dbProduct, colorImgs, variants, reviewStats, mappedReviews));
                }
                return NotFound();
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Database is temporarily unavailable." });
            }
        }

        // GET: api/products/seller/{sellerId}
        [HttpGet("seller/{sellerId}")]
        public async Task<IActionResult> GetProductsBySeller(int sellerId)
        {
            try
            {
                var products = await _db.Products
                    .AsNoTracking()
                    .Where(p => (p.Status == "active" || p.Status == "approved") && p.SellerId == sellerId)
                    .ToListAsync();
                var productIds = products.Select(p => p.ProductId).ToList();
                var colorImgs = await LoadProductColorImagesAsync(productIds);
                var dbReviews = await _db.Reviews
                    .AsNoTracking()
                    .Where(r => productIds.Contains(r.ProductId))
                    .ToListAsync();
                var variants = await _db.ProductVariants
                    .AsNoTracking()
                    .Where(v => productIds.Contains(v.ProductId))
                    .ToListAsync();

                var reviewStats = dbReviews
                    .GroupBy(r => r.ProductId)
                    .ToDictionary(g => g.Key, g => ((double)g.Average(x => x.Rating), g.Count()));
                return Ok(products.Select(p => MapDbProduct(p, colorImgs, variants, reviewStats)).ToList());
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Database is temporarily unavailable." });
            }
        }

        private static bool IsTransientDatabaseException(Exception ex)
        {
            if (ex is TimeoutException || ex is SqlException)
            {
                return true;
            }

            if (ex is InvalidOperationException ioe &&
                ioe.Message.Contains("connection from the pool", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ex.InnerException is TimeoutException || ex.InnerException is SqlException)
            {
                return true;
            }

            return ex.InnerException is InvalidOperationException innerIoe &&
                   innerIoe.Message.Contains("connection from the pool", StringComparison.OrdinalIgnoreCase);
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class SellersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuthenticatedUserContextService _authenticatedUserContextService;

        public SellersController(AppDbContext db, IAuthenticatedUserContextService authenticatedUserContextService)
        {
            _db = db;
            _authenticatedUserContextService = authenticatedUserContextService;
        }

        public sealed class ToggleSellerFollowRequest
        {
            public bool Follow { get; set; }
        }

        public sealed class SellerFollowerListItem
        {
            public int UserId { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public string Avatar { get; set; } = string.Empty;
            public DateTime FollowedAt { get; set; }
        }

        // GET: api/sellers/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSellerById(int id, CancellationToken cancellationToken)
        {
            try
            {
                var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
                var seller = await BuildSellerAsync(id, currentUser?.UserId, cancellationToken);
                return seller != null ? Ok(seller) : NotFound();
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Database is temporarily unavailable." });
            }
        }

        // GET: api/sellers
        [HttpGet]
        public async Task<IActionResult> GetAllSellers(CancellationToken cancellationToken)
        {
            try
            {
                var sellerIds = await _db.Products
                    .AsNoTracking()
                    .Where(p => p.Status == "active")
                    .Select(p => p.SellerId)
                    .Distinct()
                    .ToListAsync();

                var sellers = new List<Seller>();
                var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
                foreach (var sellerId in sellerIds)
                {
                    var seller = await BuildSellerAsync(sellerId, currentUser?.UserId, cancellationToken);
                    if (seller != null)
                    {
                        sellers.Add(seller);
                    }
                }

                return Ok(sellers);
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Database is temporarily unavailable." });
            }
        }

        [HttpGet("{id}/followers")]
        public async Task<IActionResult> GetFollowers(int id, CancellationToken cancellationToken)
        {
            try
            {
                var seller = await ResolveSellerProfileAsync(id, cancellationToken);
                if (seller is null)
                {
                    return NotFound();
                }

                var followers = await _db.SellerFollowers
                    .AsNoTracking()
                    .Where(x => x.SellerId == seller.SellerId)
                    .OrderByDescending(x => x.FollowedAt)
                    .ToListAsync(cancellationToken);

                var followerUserIds = followers
                    .Select(x => x.FollowerUserId)
                    .Distinct()
                    .ToList();

                var users = await _db.Users
                    .AsNoTracking()
                    .Where(x => followerUserIds.Contains(x.UserId))
                    .ToDictionaryAsync(x => x.UserId, cancellationToken);

                var consumers = await _db.Consumers
                    .AsNoTracking()
                    .Where(x => followerUserIds.Contains(x.UserId))
                    .ToDictionaryAsync(x => x.UserId, cancellationToken);

                var items = followers.Select(follower =>
                {
                    users.TryGetValue(follower.FollowerUserId, out var user);
                    consumers.TryGetValue(follower.FollowerUserId, out var consumer);
                    var displayName = GetFollowerDisplayName(user, consumer, follower.FollowerUserId);

                    return new SellerFollowerListItem
                    {
                        UserId = follower.FollowerUserId,
                        DisplayName = displayName,
                        Avatar = BuildAvatarUrl(displayName),
                        FollowedAt = follower.FollowedAt
                    };
                }).ToList();

                return Ok(new
                {
                    sellerId = seller.SellerId,
                    count = items.Count,
                    items
                });
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Database is temporarily unavailable." });
            }
        }

        [HttpPost("{id}/follow")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetFollowState(int id, [FromBody] ToggleSellerFollowRequest? request, CancellationToken cancellationToken)
        {
            try
            {
                var currentUser = await _authenticatedUserContextService.GetCurrentAsync(cancellationToken);
                if (currentUser?.UserId is not int currentUserId || currentUserId <= 0)
                {
                    return Unauthorized(new { message = "You must be logged in to follow a shop." });
                }

                var seller = await ResolveSellerProfileAsync(id, cancellationToken);
                if (seller is null)
                {
                    return NotFound();
                }

                if (seller.UserId == currentUserId)
                {
                    return BadRequest(new { message = "You cannot follow your own shop." });
                }

                var shouldFollow = request?.Follow ?? true;
                var existing = await _db.SellerFollowers
                    .FirstOrDefaultAsync(x => x.SellerId == seller.SellerId && x.FollowerUserId == currentUserId, cancellationToken);

                if (shouldFollow && existing is null)
                {
                    _db.SellerFollowers.Add(new SellerFollower
                    {
                        SellerId = seller.SellerId,
                        FollowerUserId = currentUserId,
                        FollowedAt = DateTime.UtcNow
                    });
                }
                else if (!shouldFollow && existing is not null)
                {
                    _db.SellerFollowers.Remove(existing);
                }

                if (_db.ChangeTracker.HasChanges())
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }

                var followerCount = await SyncSellerFollowersCountAsync(seller.SellerId, cancellationToken);
                return Ok(new
                {
                    sellerId = seller.SellerId,
                    isFollowing = shouldFollow && existing is null || shouldFollow && existing is not null,
                    followers = followerCount
                });
            }
            catch (Exception ex) when (IsTransientDatabaseException(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Database is temporarily unavailable." });
            }
        }

        private async Task<Seller?> BuildSellerAsync(int sellerId, int? currentUserId, CancellationToken cancellationToken)
        {
            var sellerProfile = await ResolveSellerProfileAsync(sellerId, cancellationToken);
            var canonicalSellerId = sellerProfile?.SellerId ?? sellerId;

            var resolvedUserId = sellerProfile?.UserId ?? sellerId;

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == resolvedUserId, cancellationToken);

            var profile = await _db.Consumers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == resolvedUserId, cancellationToken);

            var sellerProductIds = await _db.Products
                .AsNoTracking()
                .Where(p => p.SellerId == canonicalSellerId && (p.Status == "active" || p.Status == "approved"))
                .Select(p => p.ProductId)
                .ToListAsync(cancellationToken);

            if (user == null && sellerProductIds.Count == 0)
            {
                return null;
            }

            var totalProducts = sellerProductIds.Count;
            var reviewStats = sellerProductIds.Count == 0
                ? new { Rating = 0d, Count = 0 }
                : await _db.Reviews
                    .AsNoTracking()
                    .Where(r => sellerProductIds.Contains(r.ProductId))
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Rating = (double?)g.Average(x => x.Rating) ?? 0d,
                        Count = g.Count()
                    })
                    .FirstOrDefaultAsync(cancellationToken) ?? new { Rating = 0d, Count = 0 };

            var shopName = GetSellerDisplayName(user, profile, sellerProfile, sellerId);
            var location = string.IsNullOrWhiteSpace(profile?.Address) ? "Philippines" : profile.Address!;
            var joinedDate = user?.CreatedAt ?? profile?.CreatedAt ?? DateTime.UtcNow;
            var followersCount = sellerProfile?.FollowersCount ?? await SyncSellerFollowersCountAsync(canonicalSellerId, cancellationToken);
            var isFollowing = currentUserId.HasValue && currentUserId.Value > 0 && await _db.SellerFollowers
                .AsNoTracking()
                .AnyAsync(x => x.SellerId == canonicalSellerId && x.FollowerUserId == currentUserId.Value, cancellationToken);

            return new Seller
            {
                Id = canonicalSellerId,
                ShopName = shopName,
                Avatar = BuildSellerAvatar(user, sellerProfile, shopName),
                CoverImage = "/images/cover-photo/Skinny_1920x420__35_.jpg",
                Description = $"Products uploaded by {shopName}.",
                Location = location,
                Rating = reviewStats.Rating,
                TotalRatings = reviewStats.Count,
                TotalProducts = totalProducts,
                Followers = followersCount,
                IsFollowing = isFollowing,
                ResponseRate = 100,
                ResponseTime = "within a day",
                JoinedDate = joinedDate
            };
        }

        private async Task<SellerProfile?> ResolveSellerProfileAsync(int sellerId, CancellationToken cancellationToken)
        {
            return await _db.Sellers
                .Where(s => s.SellerId == sellerId || s.UserId == sellerId)
                .OrderByDescending(s => s.SellerId == sellerId)
                .ThenBy(s => s.SellerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<int> SyncSellerFollowersCountAsync(int sellerId, CancellationToken cancellationToken)
        {
            var count = await _db.SellerFollowers
                .AsNoTracking()
                .CountAsync(x => x.SellerId == sellerId, cancellationToken);

            var sellerProfile = await _db.Sellers
                .Where(x => x.SellerId == sellerId || x.UserId == sellerId)
                .OrderByDescending(x => x.SellerId == sellerId)
                .ThenBy(x => x.SellerId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sellerProfile != null && sellerProfile.FollowersCount != count)
            {
                sellerProfile.FollowersCount = count;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return count;
        }

        private static string GetFollowerDisplayName(User? user, Consumer? consumer, int userId)
        {
            if (!string.IsNullOrWhiteSpace(consumer?.Username))
            {
                return consumer.Username.Trim();
            }

            if (!string.IsNullOrWhiteSpace(consumer?.FullName))
            {
                return consumer.FullName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(user?.Email))
            {
                var atIndex = user.Email.IndexOf('@');
                return atIndex > 0 ? user.Email[..atIndex] : user.Email;
            }

            return $"User {userId}";
        }

        private static string GetSellerDisplayName(User? user, Consumer? profile, SellerProfile? sellerProfile, int sellerId)
        {
            if (!string.IsNullOrWhiteSpace(sellerProfile?.BusinessName))
            {
                return sellerProfile.BusinessName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(profile?.Username))
            {
                return profile.Username.Trim();
            }

            if (!string.IsNullOrWhiteSpace(profile?.FullName))
            {
                return profile.FullName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(user?.Email))
            {
                var atIndex = user.Email.IndexOf('@');
                return atIndex > 0 ? user.Email[..atIndex] : user.Email;
            }

            return $"Seller {sellerId}";
        }

        private static string BuildAvatarUrl(string name)
        {
            var encodedName = Uri.EscapeDataString(name);
            return $"https://ui-avatars.com/api/?name={encodedName}&background=111827&color=ffffff&size=128&bold=true";
        }

        private static string BuildSellerAvatar(User? user, SellerProfile? sellerProfile, string shopName)
        {
            if (sellerProfile?.LogoData is { Length: > 0 })
            {
                return $"/api/messages/sellers/{sellerProfile.SellerId}/avatar";
            }

            if (!string.IsNullOrWhiteSpace(sellerProfile?.LogoPath))
            {
                return NormalizeImagePath(sellerProfile.LogoPath!);
            }

            if (user?.ProfilePicture is { Length: > 0 })
            {
                var contentType = string.IsNullOrWhiteSpace(user.ProfilePictureContentType)
                    ? "image/jpeg"
                    : user.ProfilePictureContentType.Trim();
                return $"data:{contentType};base64,{Convert.ToBase64String(user.ProfilePicture)}";
            }

            return BuildAvatarUrl(shopName);
        }

        private static string NormalizeImagePath(string path)
        {
            var trimmed = path.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return trimmed;
            }

            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/", StringComparison.Ordinal))
            {
                return trimmed;
            }

            return "/" + trimmed.Replace("\\", "/").TrimStart('/');
        }

        private static bool IsTransientDatabaseException(Exception ex)
        {
            if (ex is TimeoutException || ex is SqlException)
            {
                return true;
            }

            if (ex is InvalidOperationException ioe &&
                ioe.Message.Contains("connection from the pool", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ex.InnerException is TimeoutException || ex.InnerException is SqlException)
            {
                return true;
            }

            return ex.InnerException is InvalidOperationException innerIoe &&
                   innerIoe.Message.Contains("connection from the pool", StringComparison.OrdinalIgnoreCase);
        }
    }
}
