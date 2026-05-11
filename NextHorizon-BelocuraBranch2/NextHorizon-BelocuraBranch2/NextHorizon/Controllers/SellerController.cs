using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NextHorizon.Data;
using NextHorizon.Data.Messaging;
using NextHorizon.Models;
using NextHorizon.Security;
using NextHorizon.Services;

namespace NextHorizon.Controllers;

[Authorize]
public sealed class SellerController : Controller
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly IAuthenticatedUserContextService _authenticatedUserContextService;
    private readonly IOrderConversationResolver _orderConversationResolver;
    private readonly IMessagingRepository _messagingRepository;
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SellerController> _logger;
    private readonly ISellerNotificationService _sellerNotificationService;

    public SellerController(
        IWebHostEnvironment webHostEnvironment,
        IConfiguration configuration,
        IAuthenticatedUserContextService authenticatedUserContextService,
        IOrderConversationResolver orderConversationResolver,
        IMessagingRepository messagingRepository,
        AppDbContext db,
        IMemoryCache cache,
        ILogger<SellerController> logger,
        ISellerNotificationService sellerNotificationService)
    {
        _webHostEnvironment = webHostEnvironment;
        _configuration = configuration;
        _authenticatedUserContextService = authenticatedUserContextService;
        _orderConversationResolver = orderConversationResolver;
        _messagingRepository = messagingRepository;
        _db = db;
        _cache = cache;
        _logger = logger;
        _sellerNotificationService = sellerNotificationService;
    }

    // ─── EXISTING: Seller Messenger ───────────────────────────────────────────

    [HttpGet("seller/messenger")]
    public async Task<IActionResult> SellerMessenger(
        string? mode,
        int? actorUserId,
        int? conversationId,
        int? consumerId,
        int? customerId,
        int? orderId,
        CancellationToken cancellationToken)
    {
        var requestedMode = string.Equals(mode, "dev", StringComparison.OrdinalIgnoreCase) ? "dev" : "main";
        var devMessagingEnabled = _webHostEnvironment.IsDevelopment() &&
            _configuration.GetValue("Features:EnableDevMessaging", false);
        var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId
            : 0;
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
        var sellerName = currentUser?.SellerId is int sellerId
            ? ProductData.Sellers.FirstOrDefault(item => item.Id == sellerId)?.ShopName ?? "Seller"
            : "Seller";
        var resolvedConversationId = conversationId;
        var resolvedConsumerId = consumerId ?? customerId;

        if (!resolvedConversationId.HasValue
            && orderId.HasValue
            && orderId.Value > 0
            && currentUser?.SellerId is int currentSellerId
            && currentSellerId > 0)
        {
            var actor = new MessageActorContext(
                currentUser.SellerAccountUserId ?? currentUser.UserId,
                currentUser.ConsumerId,
                currentUser.SellerId);
            var orderContext = await _orderConversationResolver.ResolveAsync(orderId.Value, actor, cancellationToken);

            if (orderContext is not null
                && orderContext.CanRequestUserAccess
                && orderContext.SellerId == currentSellerId)
            {
                var summary = await _messagingRepository.CreateOrGetOrderAsync(
                    orderContext.OrderId,
                    orderContext.BuyerConsumerId,
                    orderContext.SellerId,
                    cancellationToken);

                resolvedConversationId = summary.ConversationId;
                resolvedConsumerId ??= orderContext.BuyerConsumerId;
            }
        }

        ViewData["ApiMode"] = requestedMode == "dev" && devMessagingEnabled ? "dev" : "main";
        ViewData["ActorUserId"] = actorUserId ?? currentUserId;
        ViewData["ConversationId"] = resolvedConversationId;
        ViewData["ConsumerId"] = resolvedConsumerId;
        ViewData["OrderId"] = orderId;
        ViewData["DebugUserId"] = actorUserId;
        ViewData["DevMessagingEnabled"] = devMessagingEnabled;
        ViewData["DashboardHeading"] = "Messages";
        ViewData["SellerName"] = sellerName;

        return View("~/Views/Seller/SellerMessenger.cshtml");
    }

    [HttpGet("seller/products/{productId:int}/shared-card")]
    public async Task<IActionResult> GetSharedProductCard(int productId, CancellationToken cancellationToken)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
        if (currentUser?.SellerId is not int sellerId || sellerId <= 0)
        {
            return Forbid();
        }

        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.ProductId == productId && p.SellerId == sellerId)
            .Select(p => new
            {
                p.ProductId,
                p.ProductName,
                p.Price
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (product == null)
        {
            return NotFound();
        }

        var variants = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.Id)
            .Select(v => new
            {
                v.Id,
                v.Quantity,
                v.Price,
                HasImageData = v.ImageData != null && v.ImageData.Length > 0,
                v.ImagePath
            })
            .ToListAsync(cancellationToken);

        var livePrice = variants
            .Where(v => v.Price.HasValue && v.Price.Value > 0)
            .Select(v => v.Price!.Value)
            .DefaultIfEmpty(product.Price)
            .Min();

        var totalStock = variants.Sum(v => v.Quantity);
        var imageVariant = variants.FirstOrDefault(v =>
            v.HasImageData || !string.IsNullOrWhiteSpace(v.ImagePath));
        var imageUrl = imageVariant != null
            ? Url.Action("Variant", "ProductImage", new { variantId = imageVariant.Id }) ?? $"/ProductImage/Variant/{imageVariant.Id}"
            : string.Empty;
        var productUrl = Url.Action("ViewProduct", "Seller", new { id = product.ProductId, state = "active" }) ?? $"/Seller/ViewProduct?id={product.ProductId}&state=active";

        return Ok(new
        {
            productId = product.ProductId,
            productName = product.ProductName,
            imageUrl,
            price = livePrice,
            stock = totalStock,
            productUrl
        });
    }

    // ─── NEW: Seller Dashboard ────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var sellerId = HttpContext.Session.GetInt32("SellerId") ?? 0;
        var sellerIndexCacheKey = $"seller:products:index:{sellerId}";

        // ── STEP 4 (Apply Discounts): Load active promotions fresh on every request ──
        // Never cached — promotions must always reflect current Active status
        // Fetches from dbo.Promotions where Status = "Active" for this seller
        var activePromotions = await _db.Promotions
            .Where(p => p.SellerId == sellerId && p.Status == "Active")
            .ToListAsync();

        // Build a Dictionary<ProductId, DbPromotion> from SelectedProductIdsJson
        // SelectedProductIdsJson is stored as integer array e.g. [35, 36]
        // Only the first promotion per product is kept (first-wins rule)
        var promotionMap = new Dictionary<int, DbPromotion>();
        foreach (var promo in activePromotions)
        {
            try
            {
                // Deserialize [35, 36] → List<int> then map each productId → promotion
                var ids = System.Text.Json.JsonSerializer.Deserialize<List<int>>(promo.SelectedProductIdsJson) ?? new();
                foreach (var pid in ids)
                    if (!promotionMap.ContainsKey(pid))
                        promotionMap[pid] = promo;
            }
            catch { }
        }
        // Pass promotionMap to the view — used in Index.cshtml to compute discounted prices
        ViewBag.PromotionMap = promotionMap;

        try
        {
            var products = await _db.Products.Where(p => p.SellerId == sellerId).ToListAsync();

            var productRatings = await _db.Reviews
                .GroupBy(r => r.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    ReviewCount = g.Count(),
                    AverageRating = g.Average(r => (double)r.Rating)
                })
                .ToDictionaryAsync(x => x.ProductId);

            var variantSnapshots = await _db.ProductVariants
                .AsNoTracking()
                .GroupBy(v => v.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    ImagePath = g
                        .Where(v => !string.IsNullOrEmpty(v.ImagePath))
                        .OrderBy(v => v.Id)
                        .Select(v => v.ImagePath)
                        .FirstOrDefault(),
                    FirstVariantId = g.OrderBy(v => v.Id).Select(v => v.Id).FirstOrDefault(),
                    HasBinaryImage = g.Any(v => v.ImageData != null),
                    Price = g
                        .Where(v => v.Price.HasValue && v.Price.Value > 0)
                        .Select(v => v.Price)
                        .Min(),
                    ProductPrice = g.Select(v => v.Product!.Price).FirstOrDefault()
                })
                .ToListAsync();

            var variantMap = variantSnapshots.ToDictionary(x => x.ProductId, x => x);

            foreach (var p in products)
            {
                if (variantMap.TryGetValue(p.ProductId, out var snapshot))
                {
                    // Use binary image endpoint if available, otherwise fall back to file path
                    if (snapshot.HasBinaryImage)
                        p.ImagePath = $"/ProductImage/Variant/{snapshot.FirstVariantId}";
                    else if (!string.IsNullOrEmpty(snapshot.ImagePath))
                        p.ImagePath = snapshot.ImagePath;
                    if (snapshot.Price.HasValue && snapshot.Price.Value > 0)
                        p.Price = snapshot.Price.Value;
                }

                if (productRatings.TryGetValue(p.ProductId, out var ratingData))
                {
                    p.ReviewCount = ratingData.ReviewCount;
                    p.AverageRating = ratingData.AverageRating;
                }
                else
                {
                    p.ReviewCount = 0;
                    p.AverageRating = 5.0;
                }
            }

            _cache.Set(sellerIndexCacheKey, products, TimeSpan.FromMinutes(5));
            return View("~/Views/Dashboard/Index.cshtml", products);
        }
        catch (Exception ex) when (IsTransientDatabaseException(ex))
        {
            _logger.LogWarning(ex, "Database unreachable while loading seller product list.");

            if (_cache.TryGetValue(sellerIndexCacheKey, out List<DbProduct>? cachedProducts) && cachedProducts != null)
            {
                TempData["SuccessMessage"] = "Database is temporarily unreachable. Showing last cached product list.";
                return View("~/Views/Dashboard/Index.cshtml", cachedProducts);
            }

            TempData["SuccessMessage"] = "Database is temporarily unreachable. Showing empty product list.";
            return View("~/Views/Dashboard/Index.cshtml", new List<DbProduct>());
        }
    }

    // ─── NEW: Create Product ──────────────────────────────────────────────────

    public async Task<IActionResult> CreateProduct(string? mode, int? id)
    {
        ViewBag.Mode = mode ?? "create";
        ViewBag.ExistingVariants = "[]";

        if (id.HasValue && (mode == "edit" || mode == "renew" || mode == "relist"))
        {
            var product = await _db.Products.FindAsync(id.Value);
            if (product != null)
            {
                var variants = await _db.ProductVariants
                    .Where(v => v.ProductId == product.ProductId)
                    .ToListAsync();
                ViewBag.ExistingVariants = JsonSerializer.Serialize(variants);
                return View("~/Views/Dashboard/CreateProduct.cshtml", product);
            }
        }
        return View("~/Views/Dashboard/CreateProduct.cshtml", new DbProduct());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(
        DbProduct model,
        IFormFile? imageFile,
        string? mode,
        string[]? colorNames,
        string[]? colorStocks,
        string[]? colorSizes,
        string[]? variantSkus,
        string[]? variantAvailabilities,
        string[]? variantPrices,
        string[]? variantWeights,
        string[]? variantLengths,
        string[]? variantHeights,
        string[]? variantWidths)
    {
        try
        {
            _logger.LogInformation("=== CREATE PRODUCT START ===");

            ModelState.Remove("imageFile");
            ModelState.Remove("mode");
            ModelState.Remove("colorNames");
            ModelState.Remove("colorStocks");
            ModelState.Remove("colorSizes");
            ModelState.Remove("variantSkus");
            ModelState.Remove("variantAvailabilities");
            ModelState.Remove("variantPrices");
            ModelState.Remove("variantWeights");
            ModelState.Remove("variantLengths");
            ModelState.Remove("variantHeights");
            ModelState.Remove("variantWidths");

            if ((imageFile == null || imageFile.Length == 0) && Request.Form.Files.Count > 0)
                imageFile = Request.Form.Files.FirstOrDefault(f => f.Name == "imageFile") ?? Request.Form.Files.FirstOrDefault();

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(uploadsDir);
                var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploadsDir, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);
                model.ImagePath = "/uploads/products/" + fileName;
            }

            var colorNameList = (colorNames ?? Array.Empty<string>()).Select(n => n?.Trim()).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            _logger.LogInformation("Files received: {files}", string.Join(", ", Request.Form.Files.Select(f => $"{f.Name}:{f.FileName}")));
            var colorStockList = (colorStocks ?? Array.Empty<string>()).Select(s => int.TryParse(s, out var n) ? n : 0).ToList();
            var colorSizeList = (colorSizes ?? Array.Empty<string>()).Select(s => s?.Trim() ?? "").ToList();
            var skuList = (variantSkus ?? Array.Empty<string>()).Select(s => s?.Trim() ?? "").ToList();
            var availabilityList = (variantAvailabilities ?? Array.Empty<string>()).Select(s => s?.Trim() ?? "").ToList();
            var priceList = (variantPrices ?? Array.Empty<string>()).Select(s => decimal.TryParse(s, out var n) ? n : (decimal?)null).ToList();
            var weightList = (variantWeights ?? Array.Empty<string>()).Select(s => decimal.TryParse(s, out var n) ? n : (decimal?)null).ToList();
            var lengthList = (variantLengths ?? Array.Empty<string>()).Select(s => decimal.TryParse(s, out var n) ? n : (decimal?)null).ToList();
            var heightList = (variantHeights ?? Array.Empty<string>()).Select(s => decimal.TryParse(s, out var n) ? n : (decimal?)null).ToList();
            var widthList = (variantWidths ?? Array.Empty<string>()).Select(s => decimal.TryParse(s, out var n) ? n : (decimal?)null).ToList();

            var variants = new List<DbProductVariant>();
            var usedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var variantUploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(variantUploadsDir);

            // Read main image bytes once so all variants can reuse it as fallback
            byte[]? mainImageBytes = null;
            string? mainImageMime = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                using var mainMs = new MemoryStream();
                await imageFile.OpenReadStream().CopyToAsync(mainMs);
                mainImageBytes = mainMs.ToArray();
                mainImageMime = imageFile.ContentType;
            }

            for (int i = 0; i < colorNameList.Count; i++)
            {
                var colorName = colorNameList[i]!;
                var stock = i < colorStockList.Count ? colorStockList[i] : 0;
                var sizes = i < colorSizeList.Count ? colorSizeList[i] : null;
                var variantSkuInput = i < skuList.Count ? skuList[i] : null;
                var variantAvailabilityInput = i < availabilityList.Count ? availabilityList[i] : null;
                var variantWeight = i < weightList.Count ? weightList[i] : model.Weight;
                var variantLength = i < lengthList.Count ? lengthList[i] : model.Length;
                var variantHeight = i < heightList.Count ? heightList[i] : model.Height;
                var variantWidth = i < widthList.Count ? widthList[i] : model.Width;
                var variantSku = !string.IsNullOrWhiteSpace(variantSkuInput)
                    ? variantSkuInput!
                    : $"SKU-{DateTime.UtcNow:yyyyMMddHHmmss}-{i + 1}";

                if (usedSkus.Contains(variantSku))
                {
                    int suffix = 2;
                    var baseSku = variantSku;
                    while (usedSkus.Contains($"{baseSku}-{suffix}")) suffix++;
                    variantSku = $"{baseSku}-{suffix}";
                }
                usedSkus.Add(variantSku);

                var variantImagePath = model.ImagePath ?? string.Empty;
                byte[]? variantImageBytes = null;
                string? variantMimeType = null;

                var colorFiles = Request.Form.Files.GetFiles($"colorFiles_{i}");
                _logger.LogInformation("Variant {i}: colorFiles count = {count}", i, colorFiles.Count);
                if (colorFiles.Count > 0)
                {
                    var file = colorFiles[0];
                    if (file.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        await file.CopyToAsync(ms);
                        variantImageBytes = ms.ToArray();
                        variantMimeType = file.ContentType;

                        var fn = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
                        var fp = Path.Combine(variantUploadsDir, fn);
                        System.IO.File.WriteAllBytes(fp, variantImageBytes);
                        variantImagePath = "/uploads/products/" + fn;
                        if (string.IsNullOrEmpty(model.ImagePath)) model.ImagePath = variantImagePath;
                    }
                }
                else if (mainImageBytes != null && !string.IsNullOrEmpty(model.ImagePath))
                {
                    // Fallback: use the main product image binary for this variant
                    variantImageBytes = mainImageBytes;
                    variantMimeType = mainImageMime;
                    variantImagePath = model.ImagePath;
                }

                variants.Add(new DbProductVariant
                {
                    ProductId = model.ProductId,
                    SKU = variantSku,
                    Style = colorName,
                    Size = string.IsNullOrWhiteSpace(sizes) ? "N/A" : sizes!,
                    Quantity = stock,
                    Availability = !string.IsNullOrWhiteSpace(variantAvailabilityInput)
                        ? variantAvailabilityInput!
                        : (stock > 0 ? "In Stock" : "Pre-Order"),
                    ImagePath = variantImagePath,
                    ImageData = variantImageBytes,
                    ImageMimeType = variantMimeType,
                    Price = i < priceList.Count ? priceList[i] : (decimal?)null,
                    Weight = variantWeight,
                    Length = variantLength,
                    Height = variantHeight,
                    Width = variantWidth
                });
            }

            if (variants.Count > 0) model.SKU = variants[0].SKU;

            int productId;

            if (mode == "edit" && model.ProductId > 0)
            {
                var existing = await _db.Products.FindAsync(model.ProductId);
                if (existing != null)
                {
                    existing.ProductName = model.ProductName;
                    existing.Price = model.Price;
                    existing.Category = model.Category;
                    existing.Details = model.Details;
                    existing.Brand = model.Brand;
                    existing.SKU = model.SKU;
                    existing.Weight = model.Weight;
                    existing.Length = model.Length;
                    existing.Height = model.Height;
                    existing.Width = model.Width;
                    existing.Gender = model.Gender;
                    existing.Status = model.Status ?? "active";
                    if (!string.IsNullOrEmpty(model.ImagePath)) existing.ImagePath = model.ImagePath;
                    await _db.SaveChangesAsync();
                    await SaveProductVariantsAsync(model.ProductId, variants);
                }
                productId = model.ProductId;
            }
            else if (mode == "relist" && model.ProductId > 0)
            {
                var existing = await _db.Products.FindAsync(model.ProductId);
                if (existing != null)
                {
                    existing.Status = "active";
                    existing.ProductName = model.ProductName;
                    existing.Price = model.Price;
                    existing.Category = model.Category;
                    existing.Details = model.Details;
                    existing.Brand = model.Brand;
                    existing.SKU = model.SKU;
                    existing.Weight = model.Weight;
                    existing.Length = model.Length;
                    existing.Height = model.Height;
                    existing.Width = model.Width;
                    existing.Gender = model.Gender;
                    if (!string.IsNullOrEmpty(model.ImagePath)) existing.ImagePath = model.ImagePath;
                    await _db.SaveChangesAsync();
                    await SaveProductVariantsAsync(model.ProductId, variants);
                }
                productId = model.ProductId;
            }
            else if (mode == "renew" && model.ProductId > 0)
            {
                var existing = await _db.Products.FindAsync(model.ProductId);
                if (existing != null)
                {
                    existing.Status = "active";
                    existing.ProductName = model.ProductName;
                    existing.Price = model.Price;
                    existing.Category = model.Category;
                    existing.Details = model.Details;
                    existing.Brand = model.Brand;
                    existing.SKU = model.SKU;
                    existing.Weight = model.Weight;
                    existing.Length = model.Length;
                    existing.Height = model.Height;
                    existing.Width = model.Width;
                    existing.Gender = model.Gender;
                    if (!string.IsNullOrEmpty(model.ImagePath)) existing.ImagePath = model.ImagePath;
                    await _db.SaveChangesAsync();
                    await SaveProductVariantsAsync(model.ProductId, variants);
                }
                productId = model.ProductId;
            }
            else
            {
                model.Status = "pending";
                model.SellerId = HttpContext.Session.GetInt32("SellerId") ?? 1;
                _db.Products.Add(model);
                await _db.SaveChangesAsync();
                productId = model.ProductId;
                await SaveProductVariantsAsync(productId, variants);
            }

            if (string.IsNullOrEmpty(model.ImagePath))
            {
                var firstVariantWithImage = variants.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.ImagePath));
                if (firstVariantWithImage != null)
                {
                    model.ImagePath = firstVariantWithImage.ImagePath;
                    var productToUpdate = await _db.Products.FindAsync(productId);
                    if (productToUpdate != null)
                    {
                        productToUpdate.ImagePath = model.ImagePath;
                        await _db.SaveChangesAsync();
                    }
                }
            }

            if (mode == "edit") TempData["SuccessMessage"] = "Product updated successfully.";
            else if (mode == "relist") TempData["SuccessMessage"] = "Product relisted successfully.";
            else if (mode == "renew") TempData["SuccessMessage"] = "Product renewed successfully.";
            else TempData["SuccessMessage"] = "Product added successfully and is pending approval.";

            var sellerIdForNotification = HttpContext.Session.GetInt32("SellerId") ?? model.SellerId;
            if (sellerIdForNotification > 0)
            {
                await _sellerNotificationService.CreateAsync(new SellerNotification
                {
                    RecipientType = "seller",
                    RecipientId = sellerIdForNotification.ToString(),
                    SellerId = sellerIdForNotification,
                    Category = "system",
                    Type = mode switch
                    {
                        "edit" => "system.product_updated",
                        "relist" => "system.product_relisted",
                        "renew" => "system.product_renewed",
                        _ => "system.product_pending_approval"
                    },
                    Title = mode switch
                    {
                        "edit" => "Product Updated",
                        "relist" => "Product Relisted",
                        "renew" => "Product Renewed",
                        _ => "Product Submitted"
                    },
                    Message = mode switch
                    {
                        "edit" => $"Product {model.ProductName} was updated successfully.",
                        "relist" => $"Product {model.ProductName} was relisted successfully.",
                        "renew" => $"Product {model.ProductName} was renewed successfully.",
                        _ => $"Product {model.ProductName} was submitted and is pending approval."
                    },
                    Priority = mode is "edit" ? "medium" : "high",
                    DeliveryMode = "in_app",
                    LinkType = "product",
                    LinkTarget = $"/Seller/ViewProduct?id={productId}&state={(mode is null ? "pending" : "active")}",
                    ActionRequired = mode is null or "",
                    DeduplicationKey = mode is "edit" ? $"system.product_updated:{productId}" : null
                });
            }

            InvalidateProductCaches();

            if (mode != "edit" && mode != "relist" && mode != "renew")
                return RedirectToAction("Index", new { sizeGuideProductId = productId });

            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating/updating product");
            TempData["ErrorMessage"] = "Product save failed. Please review your variant fields and try again.";
            ViewBag.Mode = mode ?? "create";
            ViewBag.ExistingVariants = "[]";
            return View(model);
        }
    }

    // ─── NEW: View Product ────────────────────────────────────────────────────

    public async Task<IActionResult> ViewProduct(int? id, string? state)
    {
        ViewBag.State = state ?? "active";
        if (id.HasValue)
        {
            var product = await _db.Products.FindAsync(id.Value);
            if (product != null)
            {
                var variants = await _db.ProductVariants
                    .Where(v => v.ProductId == id.Value)
                    .OrderBy(v => v.Id)
                    .ToListAsync();

                await BackfillProductVariantImagesAsync(variants);

                ViewBag.AllColorImages = variants
                    .Select(GetVariantImageUrl)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .ToList();

                ViewBag.ImagesByStyle = variants
                    .GroupBy(v => string.IsNullOrWhiteSpace(v.Style) ? "Default" : v.Style)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(GetVariantImageUrl)
                            .Where(url => !string.IsNullOrWhiteSpace(url))
                            .ToList());

                var variantPrices = variants
                    .Where(v => v.Price.HasValue && v.Price.Value > 0)
                    .Select(v => v.Price!.Value)
                    .ToList();
                if (variantPrices.Count > 0) product.Price = variantPrices.Min();
                // product.Price already has the DB value if variants have no price

                ViewBag.ProductVariants = variants;
                return View("~/Views/Dashboard/ViewProduct.cshtml", product);
            }
        }
        return View("~/Views/Dashboard/ViewProduct.cshtml", new DbProduct());
    }

    // ─── NEW: Delete / Update Status ─────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var sellerId = HttpContext.Session.GetInt32("SellerId") ?? 0;
        var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == id && p.SellerId == sellerId);
        if (product != null)
        {
            product.Status = "relist";
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Product removed.";
            InvalidateProductCaches();

            await _sellerNotificationService.CreateAsync(new SellerNotification
            {
                RecipientType = "seller",
                RecipientId = sellerId.ToString(),
                SellerId = sellerId,
                Category = "system",
                Type = "system.product_removed_to_relist",
                Title = "Product Moved to Relist",
                Message = $"Product {product.ProductName} was removed and moved to relist.",
                Priority = "medium",
                DeliveryMode = "in_app",
                LinkType = "product",
                LinkTarget = $"/Seller/ViewProduct?id={product.ProductId}&state=relist",
                ActionRequired = false
            });
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var product = await _db.Products.FindAsync(id);
        if (product != null)
        {
            product.Status = status;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Status updated.";
            InvalidateProductCaches();
        }
        return RedirectToAction("Index");
    }

    // ─── NEW: Size Guide ──────────────────────────────────────────────────────

    public async Task<IActionResult> SizeGuide(int? id)
    {
        if (!id.HasValue)
        {
            TempData["ErrorMessage"] = "Please open a product from the product list before editing its size guide.";
            return RedirectToAction("Index");
        }

        var product = await _db.Products.FindAsync(id.Value);
        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found. Please select a valid product.";
            return RedirectToAction("Index");
        }

        var model = new ViewSizeGuideViewModel
        {
            ProductId = product.ProductId,
            ProductTitle = product.ProductName,
            ProductBrand = product.Brand ?? string.Empty,
            ProductDetails = product.Details ?? string.Empty,
            IsPhotoUpload = false,
            MeasurementUnit = "in",
            PhotoMeasurementUnit = "in",
            TableMeasurementUnit = "in",
            Category = product.Category ?? string.Empty,
            TableTitle = string.Empty,
            FitTips = GetDefaultFitTips(product.Category),
            HowToMeasure = GetDefaultHowToMeasure(product.Category),
            TableData = new List<List<string>>(),
            Tables = new List<SizeGuideTableItem>()
        };

        var productImageVariant = await _db.ProductVariants
            .Where(v => v.ProductId == product.ProductId && (v.ImageData != null || (v.ImagePath != null && v.ImagePath != "")))
            .OrderBy(v => v.Id)
            .FirstOrDefaultAsync();
        if (productImageVariant != null)
        {
            await BackfillProductVariantImagesAsync(new List<DbProductVariant> { productImageVariant });
            model.ProductImageUrl = GetVariantImageUrl(productImageVariant);
        }
        else
        {
            model.ProductImageUrl = string.Empty;
        }

        var existingGuide = await _db.SizeGuides
            .Include(g => g.Images)
            .FirstOrDefaultAsync(g => g.ProductId == product.ProductId);

        if (existingGuide != null)
        {
            model.TableTitle = existingGuide.Title ?? model.TableTitle;
            model.MeasurementUnit = existingGuide.MeasurementUnit ?? model.MeasurementUnit;
            model.PhotoMeasurementUnit = model.MeasurementUnit;
            model.TableMeasurementUnit = model.MeasurementUnit;
            model.Category = existingGuide.Category ?? model.Category;
            model.FitTips = string.IsNullOrWhiteSpace(existingGuide.FitTips) ? GetDefaultFitTips(model.Category) : existingGuide.FitTips;
            model.HowToMeasure = string.IsNullOrWhiteSpace(existingGuide.HowToMeasure) ? GetDefaultHowToMeasure(model.Category) : existingGuide.HowToMeasure;
            model.AdditionalNotes = existingGuide.AdditionalNotes ?? string.Empty;

            var parsedPayload = ParseSizeGuidePayload(existingGuide.TableJson, existingGuide.Title);
            model.PhotoMeasurementUnit = string.IsNullOrWhiteSpace(parsedPayload.PhotoMeasurementUnit) ? model.PhotoMeasurementUnit : parsedPayload.PhotoMeasurementUnit;
            model.TableMeasurementUnit = string.IsNullOrWhiteSpace(parsedPayload.TableMeasurementUnit) ? model.TableMeasurementUnit : parsedPayload.TableMeasurementUnit;
            model.PhotoGuideUnitsByUrl = parsedPayload.PhotoGuideUnitsByUrl;
            model.Tables = parsedPayload.Tables;
            if (model.Tables.Count > 0)
            {
                model.TableTitle = model.Tables[0].Title;
                model.TableData = model.Tables[0].Data;
            }

            model.UploadedPhotoUrls = existingGuide.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => i.ImagePath)
                .ToList();

            foreach (var table in model.Tables)
            {
                if (table.PhotoOrder <= 0 && !string.IsNullOrWhiteSpace(table.ImageUrl))
                {
                    var idx = model.UploadedPhotoUrls.FindIndex(u => string.Equals(u, table.ImageUrl, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) table.PhotoOrder = idx + 1;
                }
            }

            model.IsPhotoUpload = model.UploadedPhotoUrls.Count > 0;
        }

        return View("~/Views/Dashboard/SizeGuide.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> ViewSizeGuide(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found. Please select a valid product.";
            return RedirectToAction("Index");
        }

        var model = new ViewSizeGuideViewModel
        {
            ProductId = product.ProductId,
            ProductTitle = product.ProductName,
            ProductBrand = product.Brand ?? string.Empty,
            ProductDetails = product.Details ?? string.Empty,
            IsPhotoUpload = false,
            MeasurementUnit = "in",
            PhotoMeasurementUnit = "in",
            TableMeasurementUnit = "in",
            Category = product.Category ?? string.Empty,
            TableTitle = string.Empty,
            FitTips = GetDefaultFitTips(product.Category),
            HowToMeasure = GetDefaultHowToMeasure(product.Category),
            TableData = new List<List<string>>(),
            Tables = new List<SizeGuideTableItem>()
        };

        var productImageVariant = await _db.ProductVariants
            .Where(v => v.ProductId == product.ProductId && (v.ImageData != null || (v.ImagePath != null && v.ImagePath != "")))
            .OrderBy(v => v.Id)
            .FirstOrDefaultAsync();
        if (productImageVariant != null)
        {
            await BackfillProductVariantImagesAsync(new List<DbProductVariant> { productImageVariant });
            model.ProductImageUrl = GetVariantImageUrl(productImageVariant);
        }
        else
        {
            model.ProductImageUrl = string.Empty;
        }

        var existingGuide = await _db.SizeGuides
            .Include(g => g.Images)
            .FirstOrDefaultAsync(g => g.ProductId == product.ProductId);

        if (existingGuide != null)
        {
            model.TableTitle = existingGuide.Title ?? model.TableTitle;
            model.MeasurementUnit = existingGuide.MeasurementUnit ?? model.MeasurementUnit;
            model.PhotoMeasurementUnit = model.MeasurementUnit;
            model.TableMeasurementUnit = model.MeasurementUnit;
            model.Category = existingGuide.Category ?? model.Category;
            model.FitTips = string.IsNullOrWhiteSpace(existingGuide.FitTips) ? GetDefaultFitTips(model.Category) : existingGuide.FitTips;
            model.HowToMeasure = string.IsNullOrWhiteSpace(existingGuide.HowToMeasure) ? GetDefaultHowToMeasure(model.Category) : existingGuide.HowToMeasure;
            model.AdditionalNotes = existingGuide.AdditionalNotes ?? string.Empty;

            var parsedPayload = ParseSizeGuidePayload(existingGuide.TableJson, existingGuide.Title);
            model.PhotoMeasurementUnit = string.IsNullOrWhiteSpace(parsedPayload.PhotoMeasurementUnit) ? model.PhotoMeasurementUnit : parsedPayload.PhotoMeasurementUnit;
            model.TableMeasurementUnit = string.IsNullOrWhiteSpace(parsedPayload.TableMeasurementUnit) ? model.TableMeasurementUnit : parsedPayload.TableMeasurementUnit;
            model.PhotoGuideUnitsByUrl = parsedPayload.PhotoGuideUnitsByUrl;
            model.Tables = parsedPayload.Tables;
            if (model.Tables.Count > 0)
            {
                model.TableTitle = model.Tables[0].Title;
                model.TableData = model.Tables[0].Data;
            }

            model.UploadedPhotoUrls = existingGuide.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => i.ImagePath)
                .ToList();

            foreach (var table in model.Tables)
            {
                if (table.PhotoOrder <= 0 && !string.IsNullOrWhiteSpace(table.ImageUrl))
                {
                    var idx = model.UploadedPhotoUrls.FindIndex(u => string.Equals(u, table.ImageUrl, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) table.PhotoOrder = idx + 1;
                }
            }

            model.IsPhotoUpload = model.UploadedPhotoUrls.Count > 0;
        }

        return View(model);
    }

        [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSizeGuide(
        int productId,
        string? photoMeasurementUnit,
        string? tableMeasurementUnit,
        string? category,
        string? tableTitle,
        string? tableDataJson,
        string? additionalNotes,
        string? existingPhotoUrlsJson,
        string? removedPhotoUrlsJson,
        string? existingPhotoUnitsJson,
        string? existingPhotoUnitsByOrderJson,
        string? newPhotoUnitsJson)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return NotFound();

        var existingUrls = new List<string>();
        var removedUrls = new List<string>();
        try
        {
            if (!string.IsNullOrWhiteSpace(existingPhotoUrlsJson))
                existingUrls = JsonSerializer.Deserialize<List<string>>(existingPhotoUrlsJson) ?? new List<string>();
            if (!string.IsNullOrWhiteSpace(removedPhotoUrlsJson))
                removedUrls = JsonSerializer.Deserialize<List<string>>(removedPhotoUrlsJson) ?? new List<string>();
        }
        catch { }

        var keptUrls = existingUrls.Except(removedUrls, StringComparer.OrdinalIgnoreCase).ToList();

        Dictionary<string, string> existingPhotoUnits;
        try
        {
            existingPhotoUnits = string.IsNullOrWhiteSpace(existingPhotoUnitsJson)
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : (JsonSerializer.Deserialize<Dictionary<string, string>>(existingPhotoUnitsJson) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
        catch { existingPhotoUnits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }

        var normalizedExistingPhotoUnits = existingPhotoUnits
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
            .ToDictionary(
                kvp => kvp.Key.Trim(),
                kvp => string.Equals(kvp.Value?.Trim(), "cm", StringComparison.OrdinalIgnoreCase) ? "cm" : "in",
                StringComparer.OrdinalIgnoreCase);

        var existingPhotoUnitsByOrder = ParsePhotoGuideUnitList(existingPhotoUnitsByOrderJson);
        var newPhotoUnits = ParsePhotoGuideUnitList(newPhotoUnitsJson);

        var uploadedUrls = new List<string>();
        var uploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "sizeguide");
        Directory.CreateDirectory(uploadsDir);

        foreach (var file in Request.Form.Files)
        {
            if (file.Length > 0)
            {
                var fn = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
                var fp = Path.Combine(uploadsDir, fn);
                using var fs = new FileStream(fp, FileMode.Create);
                await file.CopyToAsync(fs);
                uploadedUrls.Add("/uploads/sizeguide/" + fn);
            }
        }

        var finalPhotoUrls = keptUrls.Concat(uploadedUrls).ToList();
        var normalizedPhotoUnit = string.Equals(photoMeasurementUnit?.Trim(), "cm", StringComparison.OrdinalIgnoreCase) ? "cm" : "in";
        var finalPhotoGuideUnitsByUrl = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < keptUrls.Count; i++)
        {
            var url = keptUrls[i];
            if (string.IsNullOrWhiteSpace(url)) continue;
            var orderUnit = i < existingPhotoUnitsByOrder.Count ? existingPhotoUnitsByOrder[i] : normalizedPhotoUnit;
            finalPhotoGuideUnitsByUrl[url] = normalizedExistingPhotoUnits.TryGetValue(url, out var unit) ? unit : orderUnit;
        }

        for (var i = 0; i < uploadedUrls.Count; i++)
        {
            var uploadedUrl = uploadedUrls[i];
            if (string.IsNullOrWhiteSpace(uploadedUrl)) continue;
            finalPhotoGuideUnitsByUrl[uploadedUrl] = i < newPhotoUnits.Count ? newPhotoUnits[i] : normalizedPhotoUnit;
        }

        string? normalizedTableJson = null;
        string? firstNonEmptyTableTitle = null;
        var parsedPayload = new SizeGuidePayload
        {
            PhotoMeasurementUnit = string.IsNullOrWhiteSpace(photoMeasurementUnit) ? string.Empty : photoMeasurementUnit.Trim(),
            TableMeasurementUnit = string.IsNullOrWhiteSpace(tableMeasurementUnit) ? string.Empty : tableMeasurementUnit.Trim()
        };

        if (!string.IsNullOrWhiteSpace(tableDataJson))
        {
            try
            {
                parsedPayload = ParseSizeGuidePayload(tableDataJson, tableTitle);
                if (string.IsNullOrWhiteSpace(parsedPayload.PhotoMeasurementUnit))
                    parsedPayload.PhotoMeasurementUnit = string.IsNullOrWhiteSpace(photoMeasurementUnit) ? string.Empty : photoMeasurementUnit.Trim();
                if (string.IsNullOrWhiteSpace(parsedPayload.TableMeasurementUnit))
                    parsedPayload.TableMeasurementUnit = string.IsNullOrWhiteSpace(tableMeasurementUnit) ? string.Empty : tableMeasurementUnit.Trim();

                var mergedPhotoUnitsByUrl = new Dictionary<string, string>(finalPhotoGuideUnitsByUrl, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in parsedPayload.PhotoGuideUnitsByUrl)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key) || !finalPhotoUrls.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase)) continue;
                    mergedPhotoUnitsByUrl[kvp.Key.Trim()] = string.Equals(kvp.Value?.Trim(), "cm", StringComparison.OrdinalIgnoreCase) ? "cm" : "in";
                }

                var fallbackPhotoUnit = string.Equals(parsedPayload.PhotoMeasurementUnit?.Trim(), "cm", StringComparison.OrdinalIgnoreCase) ? "cm" : "in";
                foreach (var url in finalPhotoUrls)
                {
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    if (!mergedPhotoUnitsByUrl.ContainsKey(url)) mergedPhotoUnitsByUrl[url] = fallbackPhotoUnit;
                }
                finalPhotoGuideUnitsByUrl = mergedPhotoUnitsByUrl;

                if (finalPhotoGuideUnitsByUrl.Count > 0) parsedPayload.PhotoGuideUnitsByUrl = finalPhotoGuideUnitsByUrl;

                if (parsedPayload.Tables.Count > 0)
                {
                    for (var i = 0; i < parsedPayload.Tables.Count; i++)
                    {
                        var table = parsedPayload.Tables[i];
                        if (string.IsNullOrWhiteSpace(table.MeasurementUnit))
                            table.MeasurementUnit = string.IsNullOrWhiteSpace(parsedPayload.TableMeasurementUnit)
                                ? (string.IsNullOrWhiteSpace(tableMeasurementUnit) ? "in" : tableMeasurementUnit.Trim())
                                : parsedPayload.TableMeasurementUnit;

                        var preferredPhotoIndex = table.PhotoOrder > 0 ? table.PhotoOrder - 1 : i;
                        if (string.IsNullOrWhiteSpace(table.ImageUrl) && preferredPhotoIndex >= 0 && preferredPhotoIndex < finalPhotoUrls.Count)
                            table.ImageUrl = finalPhotoUrls[preferredPhotoIndex];

                        if (table.PhotoOrder <= 0 && !string.IsNullOrWhiteSpace(table.ImageUrl))
                        {
                            var resolvedIndex = finalPhotoUrls.FindIndex(u => string.Equals(u, table.ImageUrl, StringComparison.OrdinalIgnoreCase));
                            if (resolvedIndex >= 0) table.PhotoOrder = resolvedIndex + 1;
                        }
                    }

                    normalizedTableJson = JsonSerializer.Serialize(parsedPayload);
                    firstNonEmptyTableTitle = parsedPayload.Tables.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Title))?.Title;
                }
                else if (!string.IsNullOrWhiteSpace(parsedPayload.PhotoMeasurementUnit) || !string.IsNullOrWhiteSpace(parsedPayload.TableMeasurementUnit) || finalPhotoGuideUnitsByUrl.Count > 0)
                {
                    parsedPayload.PhotoGuideUnitsByUrl = finalPhotoGuideUnitsByUrl;
                    normalizedTableJson = JsonSerializer.Serialize(parsedPayload);
                }
            }
            catch
            {
                if (finalPhotoGuideUnitsByUrl.Count > 0)
                {
                    parsedPayload.PhotoGuideUnitsByUrl = finalPhotoGuideUnitsByUrl;
                    normalizedTableJson = JsonSerializer.Serialize(parsedPayload);
                }
                else normalizedTableJson = null;
            }
        }
        else if (finalPhotoGuideUnitsByUrl.Count > 0)
        {
            parsedPayload.PhotoGuideUnitsByUrl = finalPhotoGuideUnitsByUrl;
            normalizedTableJson = JsonSerializer.Serialize(parsedPayload);
        }

        var sizeGuide = await _db.SizeGuides.Include(g => g.Images).FirstOrDefaultAsync(g => g.ProductId == productId);
        if (sizeGuide == null)
        {
            sizeGuide = new DbSizeGuide { ProductId = productId, CreatedAt = DateTime.UtcNow };
            _db.SizeGuides.Add(sizeGuide);
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(tableTitle) ? null : tableTitle.Trim();
        sizeGuide.Title = normalizedTitle ?? firstNonEmptyTableTitle;
        var normalizedTableUnitForPersist = string.Equals(parsedPayload.TableMeasurementUnit?.Trim(), "cm", StringComparison.OrdinalIgnoreCase) ? "cm" : "in";
        var normalizedPhotoUnitForPersist = string.Equals(parsedPayload.PhotoMeasurementUnit?.Trim(), "cm", StringComparison.OrdinalIgnoreCase) ? "cm" : "in";
        var hasPersistableTableData = parsedPayload.Tables.Any(t => HasAnyTableValue(t.Data));
        var effectiveUnit = hasPersistableTableData ? normalizedTableUnitForPersist : normalizedPhotoUnitForPersist;

        sizeGuide.MeasurementUnit = string.IsNullOrWhiteSpace(effectiveUnit) ? null : effectiveUnit;
        sizeGuide.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        sizeGuide.FitTips = GetDefaultFitTips(sizeGuide.Category);
        sizeGuide.HowToMeasure = GetDefaultHowToMeasure(sizeGuide.Category);
        sizeGuide.AdditionalNotes = string.IsNullOrWhiteSpace(additionalNotes) ? null : additionalNotes.Trim();
        sizeGuide.TableJson = normalizedTableJson;
        sizeGuide.UpdatedAt = DateTime.UtcNow;

        if (sizeGuide.Images.Any()) _db.SizeGuideImages.RemoveRange(sizeGuide.Images);

        for (var i = 0; i < finalPhotoUrls.Count; i++)
            sizeGuide.Images.Add(new DbSizeGuideImage { ImagePath = finalPhotoUrls[i], SortOrder = i });

        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Size guide saved successfully.";
        return RedirectToAction("ViewSizeGuide", new { id = productId });
    }

    // ─── NEW: View Ratings ────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> ViewRatings(int id)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id);
        if (product == null) return NotFound();

        var productVariants = await _db.ProductVariants.AsNoTracking()
            .Where(v => v.ProductId == id).OrderBy(v => v.Id).ToListAsync();

        var firstImage = productVariants.Select(v => v.ImagePath).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        if (!string.IsNullOrWhiteSpace(firstImage)) product.ImagePath = firstImage;

        var variantPrices = productVariants.Where(v => v.Price.HasValue && v.Price.Value > 0).Select(v => v.Price!.Value).ToList();
        if (variantPrices.Count > 0) product.Price = variantPrices.Min();
        // product.Price already has the DB value if variants have no price

        var reviews = await _db.Reviews.AsNoTracking()
            .Where(r => r.ProductId == id).OrderByDescending(r => r.Date).ToListAsync();

        var reviewIds = reviews.Select(r => r.Id).ToList();
        var reviewImages = reviewIds.Count == 0
            ? new List<DbReviewImage>()
            : await _db.ReviewImages.AsNoTracking().Where(i => reviewIds.Contains(i.ReviewId)).ToListAsync();

        var model = new SellerRatingsViewModel
        {
            Product = product,
            Reviews = reviews.Select(r => new SellerRatingItem
            {
                Review = r,
                Images = reviewImages.Where(i => i.ReviewId == r.Id).Select(i => i.ImageUrl).ToList()
            }).ToList()
        };

        return View("~/Views/Dashboard/ViewRatings.cshtml", model);
    }

    private sealed class SizeGuidePayload
    {
        public string PhotoMeasurementUnit { get; set; } = string.Empty;
        public string TableMeasurementUnit { get; set; } = string.Empty;
        public Dictionary<string, string> PhotoGuideUnitsByUrl { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SizeGuideTableItem> Tables { get; set; } = new();
    }

    private static SizeGuidePayload ParseSizeGuidePayload(string? rawJson, string? fallbackTitle)
    {
        var payload = new SizeGuidePayload();
        if (string.IsNullOrWhiteSpace(rawJson)) return payload;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("photoMeasurementUnit", out var photoUnitEl) && photoUnitEl.ValueKind == JsonValueKind.String)
                    payload.PhotoMeasurementUnit = (photoUnitEl.GetString() ?? string.Empty).Trim();
                if (doc.RootElement.TryGetProperty("tableMeasurementUnit", out var tableUnitEl) && tableUnitEl.ValueKind == JsonValueKind.String)
                    payload.TableMeasurementUnit = (tableUnitEl.GetString() ?? string.Empty).Trim();
                if (doc.RootElement.TryGetProperty("photoGuideUnitsByUrl", out var photoMapEl) && photoMapEl.ValueKind == JsonValueKind.Object)
                    payload.PhotoGuideUnitsByUrl = ParsePhotoGuideUnitsMap(photoMapEl);
                if (doc.RootElement.TryGetProperty("tables", out var tablesEl) && tablesEl.ValueKind == JsonValueKind.Array)
                    payload.Tables = ParseSizeGuideTablesArray(tablesEl, fallbackTitle);
                return payload;
            }
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                payload.Tables = ParseSizeGuideTablesArray(doc.RootElement, fallbackTitle);
        }
        catch { }
        return payload;
    }

    private static List<SizeGuideTableItem> ParseSizeGuideTablesArray(JsonElement rootArray, string? fallbackTitle)
    {
        var tables = new List<SizeGuideTableItem>();
        if (rootArray.ValueKind != JsonValueKind.Array) return tables;
        var items = rootArray.EnumerateArray().ToList();
        if (items.Count == 0) return tables;

        if (items[0].ValueKind == JsonValueKind.Array)
        {
            var legacyData = ParseTableData(rootArray);
            if (HasAnyTableValue(legacyData))
                tables.Add(new SizeGuideTableItem { Title = fallbackTitle?.Trim() ?? string.Empty, Data = legacyData });
            return tables;
        }

        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var title = item.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String
                ? (titleEl.GetString() ?? string.Empty).Trim() : string.Empty;
            List<List<string>> data = new();
            if (item.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                data = ParseTableData(dataEl);
            if (!HasAnyTableValue(data)) continue;
            tables.Add(new SizeGuideTableItem
            {
                Title = title,
                MeasurementUnit = item.TryGetProperty("measurementUnit", out var unitEl) && unitEl.ValueKind == JsonValueKind.String
                    ? (unitEl.GetString() ?? string.Empty).Trim() : string.Empty,
                PhotoOrder = item.TryGetProperty("photoOrder", out var orderEl) && orderEl.ValueKind == JsonValueKind.Number
                    ? orderEl.GetInt32() : 0,
                Data = data,
                ImageUrl = item.TryGetProperty("imageUrl", out var imageEl) && imageEl.ValueKind == JsonValueKind.String
                    ? (imageEl.GetString() ?? string.Empty).Trim() : string.Empty
            });
        }
        return tables;
    }

    private static List<List<string>> ParseTableData(JsonElement tableElement)
    {
        var data = new List<List<string>>();
        if (tableElement.ValueKind != JsonValueKind.Array) return data;
        foreach (var rowEl in tableElement.EnumerateArray())
        {
            if (rowEl.ValueKind != JsonValueKind.Array) continue;
            var row = new List<string>();
            foreach (var cellEl in rowEl.EnumerateArray())
                row.Add((cellEl.ToString() ?? string.Empty).Trim());
            data.Add(row);
        }
        return data;
    }

    private static Dictionary<string, string> ParsePhotoGuideUnitsMap(JsonElement mapElement)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (mapElement.ValueKind != JsonValueKind.Object) return map;
        foreach (var prop in mapElement.EnumerateObject())
        {
            var key = (prop.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key)) continue;
            var unitRaw = prop.Value.ValueKind == JsonValueKind.String
                ? (prop.Value.GetString() ?? string.Empty).Trim().ToLowerInvariant() : string.Empty;
            map[key] = unitRaw == "cm" ? "cm" : "in";
        }
        return map;
    }

    private static List<string> ParsePhotoGuideUnitList(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return new List<string>();
        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(rawJson) ?? new List<string>();
            return raw.Select(v => string.Equals(v?.Trim(), "cm", StringComparison.OrdinalIgnoreCase) ? "cm" : "in").ToList();
        }
        catch { return new List<string>(); }
    }

    private static bool HasAnyTableValue(List<List<string>> table) =>
        table.Any(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)));

    private static string GetDefaultFitTips(string? category)
    {
        return (category ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "footwear" => "If you are between sizes, choose the larger size for better comfort, especially for socks or long wear.",
            "bottoms" => "If your waist and hips suggest different sizes, choose the size based on your hip measurement for better movement.",
            "outerwear" => "If you plan to layer underneath, consider going one size up for comfort.",
            "dresses & jumpsuits" => "If your bust and hips suggest different sizes, choose the size that fits your larger measurement.",
            "activewear" => "For compression feel choose your exact size, for a relaxed feel choose one size up.",
            _ => "If you are between two sizes, choose the smaller size for a tighter fit or the larger size for a looser fit."
        };
    }

    private static string GetDefaultHowToMeasure(string? category)
    {
        return (category ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "footwear" => "Foot Length: Stand on paper and mark heel to longest toe, then measure the distance.",
            "bottoms" => "Waist: Measure around your natural waistline. Hips: Measure around the fullest part of your hips.",
            "outerwear" => "Chest: Measure around the fullest part of your chest with the tape level and relaxed.",
            "dresses & jumpsuits" => "Bust: Measure around fullest bust. Waist: Measure natural waistline. Hips: Measure fullest hip area.",
            "activewear" => "Chest: Measure fullest chest. Waist: Measure natural waist. Keep tape snug but not tight.",
            _ => "Chest: Measure around the fullest part of your chest, keeping the measuring tape horizontal."
        };
    }

    private async Task BackfillProductVariantImagesAsync(IEnumerable<DbProductVariant> variants)
    {
        var changed = false;

        foreach (var variant in variants)
        {
            if (await TryPopulateVariantImageDataAsync(variant))
                changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync();
    }

    private async Task<bool> TryPopulateVariantImageDataAsync(DbProductVariant variant)
    {
        if (variant.ImageData is { Length: > 0 } || string.IsNullOrWhiteSpace(variant.ImagePath))
            return false;

        var relativePath = variant.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath);

        if (!System.IO.File.Exists(physicalPath))
            return false;

        variant.ImageData = await System.IO.File.ReadAllBytesAsync(physicalPath);

        if (string.IsNullOrWhiteSpace(variant.ImageMimeType))
        {
            variant.ImageMimeType = Path.GetExtension(physicalPath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }

        return true;
    }

    private string GetVariantImageUrl(DbProductVariant variant)
    {
        if (variant.ImageData is { Length: > 0 } || !string.IsNullOrWhiteSpace(variant.ImagePath))
            return Url.Action("Variant", "ProductImage", new { variantId = variant.Id }) ?? $"/ProductImage/Variant/{variant.Id}";

        return string.Empty;
    }

    private async Task SaveProductVariantsAsync(int productId, List<DbProductVariant> variants)
    {
        try
        {
            var existingVariants = await _db.ProductVariants.Where(v => v.ProductId == productId).ToListAsync();
            if (existingVariants.Any())
            {
                _db.ProductVariants.RemoveRange(existingVariants);
                await _db.SaveChangesAsync();
            }

            if (variants.Count > 0)
            {
                foreach (var variant in variants)
                {
                    _db.ProductVariants.Add(new DbProductVariant
                    {
                        ProductId = productId,
                        SKU = string.IsNullOrWhiteSpace(variant.SKU) ? $"SKU-{productId}" : variant.SKU,
                        Style = string.IsNullOrWhiteSpace(variant.Style) ? "N/A" : variant.Style,
                        Size = string.IsNullOrWhiteSpace(variant.Size) ? "N/A" : variant.Size,
                        Quantity = variant.Quantity,
                        Availability = string.IsNullOrWhiteSpace(variant.Availability)
                            ? (variant.Quantity > 0 ? "In Stock" : "Pre-Order")
                            : variant.Availability,
                        ImagePath = variant.ImagePath ?? string.Empty,
                        ImageData = variant.ImageData,
                        ImageMimeType = variant.ImageMimeType,
                        Price = variant.Price,
                        Weight = variant.Weight,
                        Length = variant.Length,
                        Height = variant.Height,
                        Width = variant.Width
                    });
                }
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving variants for product {ProductId}", productId);
            throw;
        }
    }

    private void InvalidateProductCaches()
    {
        _cache.Remove("products:all");
        _cache.Remove("products:men");
        _cache.Remove("products:women");
        _cache.Remove("seller:products:index");
    }

    private static bool IsTransientDatabaseException(Exception ex)
    {
        if (ex is TimeoutException || ex is SqlException) return true;
        if (ex is InvalidOperationException ioe && ioe.Message.Contains("connection from the pool", StringComparison.OrdinalIgnoreCase)) return true;
        if (ex.InnerException is TimeoutException || ex.InnerException is SqlException) return true;
        return ex.InnerException is InvalidOperationException innerIoe &&
               innerIoe.Message.Contains("connection from the pool", StringComparison.OrdinalIgnoreCase);
    }
}


