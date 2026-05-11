using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyAspNetApp.Data;
using MyAspNetApp.Data.Messaging;
using MyAspNetApp.Models;
using MyAspNetApp.Models.ViewModels;
using MyAspNetApp.Security;
using MyAspNetApp.Services;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;

namespace MyAspNetApp.Controllers
{
    public class SellerController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SellerController> _logger;
        private readonly IAuthenticatedUserContextService _authUserContextService;
        private readonly MediaPathService _mediaPathService;
        private readonly IConfiguration _configuration;
        private readonly IMessagingRepository _messagingRepository;

        public SellerController(AppDbContext db, IWebHostEnvironment env, IMemoryCache cache, ILogger<SellerController> logger, IAuthenticatedUserContextService authUserContextService, MediaPathService mediaPathService, IConfiguration configuration, IMessagingRepository messagingRepository)
        {
            _db = db;
            _env = env;
            _cache = cache;
            _logger = logger;
            _authUserContextService = authUserContextService;
            _mediaPathService = mediaPathService;
            _configuration = configuration;
            _messagingRepository = messagingRepository;
        }

        [HttpGet("Seller/SellerMessenger")]
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
            var devMessagingEnabled = _env.IsDevelopment() &&
                _configuration.GetValue("Features:EnableDevMessaging", false);
            var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                ? parsedUserId
                : HttpContext.Session.GetInt32("UserId") ?? 0;
            var currentUser = await _authUserContextService.GetCurrentAsync(cancellationToken);
            if (currentUser?.SellerId is not int sellerId || sellerId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var sellerName = HttpContext.Session.GetString("SellerName");
            if (string.IsNullOrWhiteSpace(sellerName))
            {
                sellerName = await _db.Sellers
                    .AsNoTracking()
                    .Where(item => item.SellerId == sellerId)
                    .Select(item => item.BusinessName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var resolvedConversationId = conversationId;
            var resolvedConsumerId = consumerId ?? customerId;

            if (!resolvedConversationId.HasValue
                && orderId.HasValue
                && orderId.Value > 0
                && resolvedConsumerId.HasValue
                && resolvedConsumerId.Value > 0)
            {
                var summary = await _messagingRepository.CreateOrGetOrderAsync(
                    orderId.Value,
                    resolvedConsumerId.Value,
                    sellerId,
                    cancellationToken);

                resolvedConversationId = summary.ConversationId;
            }

            ViewData["ApiMode"] = requestedMode == "dev" && devMessagingEnabled ? "dev" : "main";
            ViewData["ActorUserId"] = actorUserId ?? currentUserId;
            ViewData["ConversationId"] = resolvedConversationId;
            ViewData["ConsumerId"] = resolvedConsumerId;
            ViewData["OrderId"] = orderId;
            ViewData["DebugUserId"] = actorUserId;
            ViewData["DevMessagingEnabled"] = devMessagingEnabled;
            ViewData["DashboardHeading"] = "Messages";
            ViewData["SellerName"] = string.IsNullOrWhiteSpace(sellerName) ? "Seller" : sellerName;

            return View("~/Views/Seller/SellerMessenger.cshtml");
        }

        [HttpGet("/api/seller/notifications")]
        public async Task<IActionResult> GetSellerNotifications([FromQuery] int? take, CancellationToken cancellationToken)
        {
            var sellerId = await GetCurrentSellerIdAsync(cancellationToken);
            if (sellerId is not int currentSellerId || currentSellerId <= 0)
            {
                return Unauthorized();
            }

            await EnsureSellerNotificationsAsync(currentSellerId, cancellationToken);

            var limit = Math.Clamp(take ?? 12, 1, 50);
            var notifications = await LoadSellerNotificationsAsync(currentSellerId, limit, cancellationToken);
            return Json(notifications);
        }

        [HttpGet("/api/seller/notifications/unread-count")]
        public async Task<IActionResult> GetSellerUnreadNotificationCount(CancellationToken cancellationToken)
        {
            var sellerId = await GetCurrentSellerIdAsync(cancellationToken);
            if (sellerId is not int currentSellerId || currentSellerId <= 0)
            {
                return Unauthorized();
            }

            await EnsureSellerNotificationsAsync(currentSellerId, cancellationToken);

                        var count = await _db.Database.SqlQueryRaw<int>(
                                        """
                                        SELECT COUNT(1) AS Value
                                        FROM dbo.Notifications
                                        WHERE RecipientType = N'Seller'
                                            AND RecipientId = {0}
                                            AND IsRead = 0
                                        """,
                                        currentSellerId)
                                .SingleAsync(cancellationToken);

            return Json(new { unreadCount = count });
        }

        [HttpPost("/api/seller/notifications/read-all")]
        public async Task<IActionResult> MarkAllSellerNotificationsRead(CancellationToken cancellationToken)
        {
            var sellerId = await GetCurrentSellerIdAsync(cancellationToken);
            if (sellerId is not int currentSellerId || currentSellerId <= 0)
            {
                return Unauthorized();
            }

            await EnsureSellerNotificationsAsync(currentSellerId, cancellationToken);

                        await _db.Database.ExecuteSqlRawAsync(
                                """
                                UPDATE dbo.Notifications
                                SET IsRead = 1
                                WHERE RecipientType = N'Seller'
                                    AND RecipientId = {0}
                                """,
                                new object[] { currentSellerId },
                                cancellationToken);

            return Json(new { success = true });
        }

        [HttpPost("/api/seller/notifications/{notificationId:int}/read")]
        public async Task<IActionResult> MarkSellerNotificationRead(int notificationId, CancellationToken cancellationToken)
        {
            var sellerId = await GetCurrentSellerIdAsync(cancellationToken);
            if (sellerId is not int currentSellerId || currentSellerId <= 0)
            {
                return Unauthorized();
            }

            await EnsureSellerNotificationsAsync(currentSellerId, cancellationToken);

                        await _db.Database.ExecuteSqlRawAsync(
                                """
                                UPDATE dbo.Notifications
                                SET IsRead = 1
                                WHERE NotificationId = {0}
                                    AND RecipientType = N'Seller'
                                    AND RecipientId = {1}
                                """,
                                new object[] { notificationId, currentSellerId },
                                cancellationToken);

            return Json(new { success = true });
        }

        [HttpGet("seller/products/{productId:int}/shared-card")]
        public async Task<IActionResult> GetSharedProductCard(int productId, CancellationToken cancellationToken)
        {
            var sellerId = await GetCurrentSellerIdAsync(cancellationToken);
            if (!sellerId.HasValue || sellerId.Value <= 0)
            {
                return Forbid();
            }

            var product = await _db.Products
                .AsNoTracking()
                .Where(p => p.ProductId == productId && p.SellerId == sellerId.Value)
                .Select(p => new
                {
                    p.ProductId,
                    p.ProductName,
                    p.Price,
                    p.ImagePath
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
            var imageUrl = imageVariant?.HasImageData == true
                ? Url.Action("GetVariantImage", "Products", new { variantId = imageVariant.Id }) ?? $"/api/products/variant-image/{imageVariant.Id}"
                : NormalizeImagePath(imageVariant?.ImagePath ?? product.ImagePath);
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

        // GET: /Seller - Seller Dashboard (loads products from DB)
        public async Task<IActionResult> Index()
        {
            var sellerId = await GetCurrentSellerIdAsync(HttpContext.RequestAborted);
            if (!sellerId.HasValue)
            {
                return RedirectToAction("Index", "Home");
            }

            var sellerIndexCacheKey = $"seller:products:index:{sellerId.Value}";
            try
            {
                var products = await LoadSellerProductsWithImagesAsync(sellerId.Value);

                // also fetch first variant image per product so UI can show something
                var variantImages = await _db.ProductVariants
                    .Where(v => !string.IsNullOrEmpty(v.ImagePath))
                    .GroupBy(v => v.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        ImagePath = g.OrderBy(v => v.Id).Select(v => v.ImagePath).FirstOrDefault()
                    })
                    .ToListAsync();

                var imageMap = variantImages.ToDictionary(x => x.ProductId, x => x.ImagePath);
                foreach (var p in products)
                {
                    if (imageMap.TryGetValue(p.ProductId, out var img) && !string.IsNullOrEmpty(img))
                    {
                        p.ImagePath = img;
                    }

                    p.ImagePath = NormalizeImagePath(p.ImagePath);
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

        public async Task<IActionResult> PendingProducts()
        {
            var sellerId = await GetCurrentSellerIdAsync(HttpContext.RequestAborted);
            if (!sellerId.HasValue)
            {
                return RedirectToAction("Index", "Home");
            }

            var products = await LoadSellerProductsWithImagesAsync(sellerId.Value);
            var pendingProducts = products
                .Where(p => string.Equals(p.Status, "pending", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return View(pendingProducts);
        }

        public async Task<IActionResult> ApprovedProducts()
        {
            var sellerId = await GetCurrentSellerIdAsync(HttpContext.RequestAborted);
            if (!sellerId.HasValue)
            {
                return RedirectToAction("Index", "Home");
            }

            var products = await LoadSellerProductsWithImagesAsync(sellerId.Value);
            var approvedProducts = products
                .Where(p => string.Equals(p.Status, "approved", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.Status, "active", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return View(approvedProducts);
        }

        // GET: /Seller/CreateProduct
        public async Task<IActionResult> CreateProduct(string? mode, int? id)
        {
            var sellerId = await GetCurrentSellerIdAsync(HttpContext.RequestAborted);
            if (!sellerId.HasValue)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Mode = mode ?? "create";
            ViewBag.ExistingColorImages = "{}";
            ViewBag.ExistingVariants = "[]";

            if (id.HasValue && (mode == "edit" || mode == "renew" || mode == "relist"))
            {
                var product = await _db.Products.FindAsync(id.Value);
                if (product != null)
                {
                    if (product.SellerId != sellerId.Value)
                    {
                        return Forbid();
                    }

                    var colorImages = await _db.ProductColorImages
                        .Where(ci => ci.ProductId == product.ProductId)
                        .ToListAsync();
                    var grouped = colorImages
                        .GroupBy(ci => ci.ColorName)
                        .ToDictionary(g => g.Key, g => g.Select(ci => NormalizeImagePath(ci.ImagePath)).ToList());
                    ViewBag.ExistingColorImages = System.Text.Json.JsonSerializer.Serialize(grouped);

                    var variants = await _db.ProductVariants
                        .Where(v => v.ProductId == product.ProductId)
                        .ToListAsync();
                    ViewBag.ExistingVariants = System.Text.Json.JsonSerializer.Serialize(variants);

                    return View(product);
                }
            }
            return View(new DbProduct());
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
            if (model == null)
                return BadRequest();

            var sellerId = await GetCurrentSellerIdAsync(HttpContext.RequestAborted);
            if (!sellerId.HasValue)
            {
                return RedirectToAction("Index", "Home");
            }

            model.SellerId = sellerId.Value;

            try
            {
                _logger.LogInformation($"=== CREATE PRODUCT START ===");
                _logger.LogInformation($"ModelState.IsValid: {ModelState.IsValid}");
                
                foreach (var ms in ModelState.Values)
                {
                    foreach (var error in ms.Errors)
                    {
                        _logger.LogWarning($"ModelState Error: {error.ErrorMessage}");
                    }
                }

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

                _logger.LogInformation($"Mode: {mode}, ColorNames count: {colorNames?.Length ?? 0}");
                if (colorNames != null)
                {
                    for (int i = 0; i < colorNames.Length; i++)
                    {
                        var stockValue = (colorStocks != null && i < colorStocks.Length) ? colorStocks[i] : null;
                        var sizeValue = (colorSizes != null && i < colorSizes.Length) ? colorSizes[i] : null;
                        _logger.LogInformation($"  [{i}] Color: {colorNames[i]}, Stock: {stockValue}, Sizes: {sizeValue}");
                    }
                }

                // Fallback when browser sends multiple files and default binding doesn't populate imageFile.
                if ((imageFile == null || imageFile.Length == 0) && Request.Form.Files.Count > 0)
                {
                    imageFile = Request.Form.Files.FirstOrDefault(f => f.Name == "imageFile")
                                ?? Request.Form.Files.FirstOrDefault();
                }

                byte[]? mainImageData = null;
                string? mainImageMimeType = null;

                // Handle main product image
                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "products");
                    Directory.CreateDirectory(uploadsDir);
                    var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(imageFile.FileName);
                    var filePath = Path.Combine(uploadsDir, fileName);
                    using var memoryStream = new MemoryStream();
                    await imageFile.CopyToAsync(memoryStream);
                    mainImageData = memoryStream.ToArray();
                    mainImageMimeType = NormalizeImageMimeType(imageFile.ContentType, fileName);
                    await System.IO.File.WriteAllBytesAsync(filePath, mainImageData, HttpContext.RequestAborted);
                    model.ImagePath = "/uploads/products/" + fileName;
                }

                // Build per-color variants for normalization (stored in ProductVariants table)
                var colorNameList = (colorNames ?? Array.Empty<string>())
                    .Select(n => n?.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!)
                    .ToList();

                var colorStockList = (colorStocks ?? Array.Empty<string>())
                    .Select(s => int.TryParse(s, out var n) ? n : 0).ToList();

                var colorSizeList = (colorSizes ?? Array.Empty<string>())
                    .Select(s => s?.Trim() ?? "")
                    .ToList();

                var skuList = (variantSkus ?? Array.Empty<string>())
                    .Select(s => s?.Trim() ?? "")
                    .ToList();

                var availabilityList = (variantAvailabilities ?? Array.Empty<string>())
                    .Select(s => s?.Trim() ?? "")
                    .ToList();

var priceList = (variantPrices ?? Array.Empty<string>())
                    .Select(s => decimal.TryParse(s, out var n) ? n : (decimal?)null)
                    .ToList();

                var weightList = (variantWeights ?? Array.Empty<string>())
                    .Select(s => decimal.TryParse(s, out var n) ? n : (decimal?)null)
                    .ToList();

                var lengthList = (variantLengths ?? Array.Empty<string>())
                    .Select(s => decimal.TryParse(s, out var n) ? n : (decimal?)null)
                    .ToList();

                var heightList = (variantHeights ?? Array.Empty<string>())
                    .Select(s => decimal.TryParse(s, out var n) ? n : (decimal?)null)
                    .ToList();

                var widthList = (variantWidths ?? Array.Empty<string>())
                    .Select(s => decimal.TryParse(s, out var n) ? n : (decimal?)null)
                    .ToList();

                _logger.LogInformation($"Processed variants - Names: {colorNameList.Count}, Stocks: {colorStockList.Count}, Sizes: {colorSizeList.Count}");

                var variants = new List<DbProductVariant>();
                var usedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < colorNameList.Count; i++)
                {
                    var colorName = colorNameList[i]!;
                    var stock = (i < colorStockList.Count) ? colorStockList[i] : 0;
                    var sizes = (i < colorSizeList.Count) ? colorSizeList[i] : null;
                    var variantSkuInput = (i < skuList.Count) ? skuList[i] : null;
                    var variantAvailabilityInput = (i < availabilityList.Count) ? availabilityList[i] : null;
                    var variantWeight = (i < weightList.Count) ? weightList[i] : model.Weight;
                    var variantLength = (i < lengthList.Count) ? lengthList[i] : model.Length;
                    var variantHeight = (i < heightList.Count) ? heightList[i] : model.Height;
                    var variantWidth = (i < widthList.Count) ? widthList[i] : model.Width;
                    var variantSku = !string.IsNullOrWhiteSpace(variantSkuInput)
                        ? variantSkuInput!
                        : $"SKU-{DateTime.UtcNow:yyyyMMddHHmmss}-{i + 1}";

                    if (usedSkus.Contains(variantSku))
                    {
                        int suffix = 2;
                        var baseSku = variantSku;
                        while (usedSkus.Contains($"{baseSku}-{suffix}"))
                        {
                            suffix++;
                        }
                        variantSku = $"{baseSku}-{suffix}";
                    }
                    usedSkus.Add(variantSku);

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
                        ImagePath = model.ImagePath ?? string.Empty,
                        ImageData = mainImageData,
                        ImageMimeType = mainImageMimeType,
                        Price = (i < priceList.Count) ? priceList[i] : (decimal?)null,
                        Weight = variantWeight,
                        Length = variantLength,
                        Height = variantHeight,
                        Width = variantWidth
                    });
                    _logger.LogInformation($"Variant {i}: SKU={variantSku}, Style={colorName}, Qty={stock}, Size={sizes}, Price={(i < priceList.Count ? priceList[i]?.ToString() : "<none>")}, W/L/H/W={variantWeight}/{variantLength}/{variantHeight}/{variantWidth}");
                }

                // Keep product-level SKU as the first variant SKU for backward compatibility.
                if (variants.Count > 0)
                {
                    model.SKU = variants[0].SKU;
                }

                int productId;

                if (mode == "edit" && model.ProductId > 0)
                {
                    var existing = await _db.Products.FindAsync(model.ProductId);
                if (existing != null)
                {
                    if (existing.SellerId != sellerId.Value)
                    {
                        return Forbid();
                    }

                    existing.ProductName = model.ProductName;
                        existing.Price = model.Price;
                        // existing.Discount = model.Discount; // handled via variants now
                        existing.Category = model.Category;
                        existing.Details = model.Details;
                        existing.Brand = model.Brand;
                        existing.SKU = model.SKU;
                        existing.Weight = model.Weight;
                        existing.Length = model.Length;
                        existing.Height = model.Height;
                        existing.Width = model.Width;
                        // Stock/sizes moved to variants – do not keep on product
                        // existing.Stock = model.Stock;
                        // existing.Sizes = model.Sizes;
                        existing.Gender = model.Gender; // new field
                        existing.Status = model.Status ?? "active";
                        if (!string.IsNullOrEmpty(model.ImagePath))
                            existing.ImagePath = model.ImagePath;
                        await _db.SaveChangesAsync();
                        _logger.LogInformation($"Updated product {model.ProductId}");

                        // Sync variants (normalize color/size/stock data)
                        await SaveProductVariantsAsync(model.ProductId, variants);
                    }
                    productId = model.ProductId;
                }
                else if (mode == "relist" && model.ProductId > 0)
                {
                var existing = await _db.Products.FindAsync(model.ProductId);
                if (existing != null)
                {
                    if (existing.SellerId != sellerId.Value)
                    {
                        return Forbid();
                    }

                    existing.Status = "active";
                        existing.ProductName = model.ProductName;
                        existing.Price = model.Price;
                        // existing.Discount = model.Discount;
                        existing.Category = model.Category;
                        existing.Details = model.Details;
                        existing.Brand = model.Brand;
                        existing.SKU = model.SKU;
                        existing.Weight = model.Weight;
                        existing.Length = model.Length;
                        existing.Height = model.Height;
                        existing.Width = model.Width;
                        //existing.Stock = model.Stock;
                        //existing.Sizes = model.Sizes;
                        existing.Gender = model.Gender;
                        if (!string.IsNullOrEmpty(model.ImagePath))
                            existing.ImagePath = model.ImagePath;
                        await _db.SaveChangesAsync();
                        _logger.LogInformation($"Relisted product {model.ProductId}");

                        // Sync variants (normalize color/size/stock data)
                        await SaveProductVariantsAsync(model.ProductId, variants);
                    }
                    productId = model.ProductId;
                }
                else
                {
                    model.Status = "pending";
                    _db.Products.Add(model);
                    await _db.SaveChangesAsync();
                    productId = model.ProductId;
                    _logger.LogInformation($"Created new product {productId}");
                    _logger.LogInformation($"About to save {variants.Count} variants");
                    await SaveProductVariantsAsync(productId, variants);
                    _logger.LogInformation($"Variants saved successfully");
                }

            // Save color images
            if (colorNameList.Count > 0)
            {
                // Remove old color images for this product
                var oldImages = _db.ProductColorImages.Where(ci => ci.ProductId == productId);
                _db.ProductColorImages.RemoveRange(oldImages);
                await _db.SaveChangesAsync();

                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(uploadsDir);

                var colorImageDataByColor = new Dictionary<string, (byte[] Bytes, string? MimeType)>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < colorNameList.Count; i++)
                {
                    var colorName = colorNameList[i];

                    // Re-insert existing images that were kept
                    if (Request.Form.TryGetValue($"existingColorPaths_{i}", out var existingPaths))
                    {
                        var paths = existingPaths.ToString();
                        if (!string.IsNullOrEmpty(paths))
                        {
                            foreach (var path in paths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                _db.ProductColorImages.Add(new DbProductColorImage
                                {
                                    ProductId = productId,
                                    ColorName = colorName,
                                    ImagePath = path
                                });

                                if (!colorImageDataByColor.ContainsKey(colorName))
                                {
                                    var existingImageData = await TryReadPublicImageDataAsync(path, HttpContext.RequestAborted);
                                    if (existingImageData.Bytes != null)
                                    {
                                        colorImageDataByColor[colorName] = (existingImageData.Bytes, existingImageData.MimeType);
                                    }
                                }
                            }
                        }
                    }

                    // Save newly uploaded files for this color
                    var colorFiles = Request.Form.Files.GetFiles($"colorFiles_{i}");
                    foreach (var file in colorFiles)
                    {
                        if (file.Length > 0)
                        {
                            var fn = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
                            var fp = Path.Combine(uploadsDir, fn);
                            using var memoryStream = new MemoryStream();
                            await file.CopyToAsync(memoryStream);
                            var bytes = memoryStream.ToArray();
                            await System.IO.File.WriteAllBytesAsync(fp, bytes, HttpContext.RequestAborted);
                            _db.ProductColorImages.Add(new DbProductColorImage
                            {
                                ProductId = productId,
                                ColorName = colorName,
                                ImagePath = "/uploads/products/" + fn
                            });

                            if (!colorImageDataByColor.ContainsKey(colorName))
                            {
                                colorImageDataByColor[colorName] = (bytes, NormalizeImageMimeType(file.ContentType, fn));
                            }
                        }
                    }
                }
                await _db.SaveChangesAsync();

                // Sync ProductVariants.imagePath using first image per style/color.
                var styleImageLookup = await _db.ProductColorImages
                    .Where(ci => ci.ProductId == productId)
                    .GroupBy(ci => ci.ColorName)
                    .Select(g => new { Style = g.Key, ImagePath = g.OrderBy(x => x.Id).Select(x => x.ImagePath).FirstOrDefault() })
                    .ToListAsync();

                var productVariants = await _db.ProductVariants
                    .Where(v => v.ProductId == productId)
                    .ToListAsync();

                foreach (var variant in productVariants)
                {
                    var matched = styleImageLookup.FirstOrDefault(x => string.Equals(x.Style, variant.Style, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(matched?.ImagePath))
                    {
                        variant.ImagePath = matched.ImagePath!;
                        if (colorImageDataByColor.TryGetValue(variant.Style, out var imageData))
                        {
                            variant.ImageData = imageData.Bytes;
                            variant.ImageMimeType = imageData.MimeType;
                        }
                        else
                        {
                            var fileImageData = await TryReadPublicImageDataAsync(matched.ImagePath!, HttpContext.RequestAborted);
                            if (fileImageData.Bytes != null)
                            {
                                variant.ImageData = fileImageData.Bytes;
                                variant.ImageMimeType = fileImageData.MimeType;
                            }
                        }
                    }
                }

                await _db.SaveChangesAsync();

                // Set product main image from first color's first photo if not already set
                if (string.IsNullOrEmpty(model.ImagePath))
                {
                    var firstColorImage = await _db.ProductColorImages
                        .Where(ci => ci.ProductId == productId)
                        .OrderBy(ci => ci.Id)
                        .FirstOrDefaultAsync();
                    if (firstColorImage != null)
                    {
                        var product = await _db.Products.FindAsync(productId);
                        if (product != null)
                        {
                            product.ImagePath = firstColorImage.ImagePath;
                            await _db.SaveChangesAsync();
                        }
                    }
                }
            }

            if (mode == "edit")
                TempData["SuccessMessage"] = "Product updated successfully.";
            else if (mode == "relist")
                TempData["SuccessMessage"] = "Product relisted successfully.";
            else
                TempData["SuccessMessage"] = "Product submitted and is pending approval.";

            InvalidateProductCaches(sellerId);
            return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating/updating product");
                TempData["ErrorMessage"] = "Product save failed. Please review your variant fields and try again.";
                ViewBag.Mode = mode ?? "create";
                ViewBag.ExistingColorImages = "{}";
                ViewBag.ExistingVariants = "[]";
                return View(model);
            }
        }

        // GET: /Seller/ViewProduct?id=1
        public async Task<IActionResult> ViewProduct(int? id, string? state)
        {
            ViewBag.State = state ?? "active";
            var sellerId = await GetCurrentSellerIdAsync(HttpContext.RequestAborted);
            if (!sellerId.HasValue)
            {
                return RedirectToAction("Index", "Home");
            }
            if (id.HasValue)
            {
                var product = await _db.Products.FindAsync(id.Value);
                if (product != null)
                {
                    if (product.SellerId != sellerId.Value)
                    {
                        return Forbid();
                    }

                    var colorImages = await _db.ProductColorImages
                        .Where(ci => ci.ProductId == id.Value)
                        .ToListAsync();

                    // keep flat list for main gallery, plus grouping by style for variant cards
                    ViewBag.AllColorImages = colorImages.Select(ci => NormalizeImagePath(ci.ImagePath)).Distinct().ToList();
                    ViewBag.ImagesByStyle = colorImages
                        .GroupBy(ci => ci.ColorName)
                        .ToDictionary(g => g.Key, g => g.Select(ci => NormalizeImagePath(ci.ImagePath)).ToList());

                    var variants = await _db.ProductVariants
                        .Where(v => v.ProductId == id.Value)
                        .ToListAsync();
                    foreach (var variant in variants)
                    {
                        variant.ImagePath = NormalizeImagePath(variant.ImagePath);
                    }
                    product.ImagePath = NormalizeImagePath(product.ImagePath);
                    ViewBag.ProductVariants = variants;

                    return View(product);
                }
            }
            return View(new DbProduct());
        }

        // POST: /Seller/DeleteProduct
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var sellerId = await GetCurrentSellerIdAsync(HttpContext.RequestAborted);
            if (!sellerId.HasValue)
            {
                return RedirectToAction("Index", "Home");
            }

            var product = await _db.Products.FindAsync(id);
            if (product != null)
            {
                if (product.SellerId != sellerId.Value)
                {
                    return Forbid();
                }

                product.Status = "relist";
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Product removed.";
                InvalidateProductCaches(sellerId);
            }
            return RedirectToAction("Index");
        }

        // POST: /Seller/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var sellerId = await GetCurrentSellerIdAsync(HttpContext.RequestAborted);
            if (!sellerId.HasValue)
            {
                return RedirectToAction("Index", "Home");
            }

            var product = await _db.Products.FindAsync(id);
            if (product != null)
            {
                if (product.SellerId != sellerId.Value)
                {
                    return Forbid();
                }

                product.Status = status;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Status updated.";
                InvalidateProductCaches(sellerId);
            }
            return RedirectToAction("Index");
        }

        // GET: /Seller/SizeGuide
        public IActionResult SizeGuide()
        {
            return View();
        }

        // GET: /Seller/ViewSizeGuide?id=1
        [HttpGet]
        public async Task<IActionResult> ViewSizeGuide(int id)
        {
            var product = await _db.Products.FindAsync(id);
            var model = new MyAspNetApp.Models.ViewSizeGuideViewModel
            {
                ProductId = id,
                ProductTitle = product?.ProductName ?? "Product Size Guide",
                IsPhotoUpload = false,
                MeasurementUnit = "in",
                Category = product?.Category ?? "Tops",
                TableTitle = "Size Chart",
                FitTips = "If you're on the borderline between two sizes, order the smaller size for a tighter fit or the larger size for a looser fit.",
                HowToMeasure = "Chest: Measure around the fullest part of your chest, keeping the measuring tape horizontal.",
                TableData = new List<List<string>>
                {
                    new List<string> { "Size", "XXS", "XS", "S", "M", "L", "XL", "XXL" },
                    new List<string> { "Chest (in.)", "28.5–30", "30–32", "32–33.5", "33.5–35", "35–37.5", "37.5–40", "40–42.5" },
                    new List<string> { "Waist (in.)", "24.5–26", "26–27", "27–28", "28–29.5", "29.5–31.5", "31.5–33.5", "33.5–35.5" },
                    new List<string> { "Hip (in.)", "33–34", "34–35", "35–36.5", "36.5–38", "38–40", "40–42", "42–44" }
                }
            };
            return View(model);
        }

        // GET: /Seller/ViewRatings?id=1
        [HttpGet]
        public async Task<IActionResult> ViewRatings(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var reviews = await _db.Reviews
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.Date)
                .ToListAsync();

            var reviewIds = reviews.Select(r => r.Id).ToList();
            var reviewImages = await _db.ReviewImages
                .Where(i => reviewIds.Contains(i.ReviewId))
                .ToListAsync();

            var model = new SellerRatingsViewModel
            {
                Product = product,
                Reviews = reviews.Select(r => new SellerRatingItem
                {
                    Review = r,
                    Images = reviewImages
                        .Where(i => i.ReviewId == r.Id)
                        .Select(i => i.ImageUrl)
                        .ToList()
                }).ToList()
            };

            model.Product.ImagePath = NormalizeImagePath(model.Product.ImagePath);

            return View(model);
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

        private async Task SaveProductVariantsAsync(int productId, List<DbProductVariant> variants)
        {
            try
            {
                // Remove existing variants for this product and replace with the current set
                var existingVariants = await _db.ProductVariants
                    .Where(v => v.ProductId == productId)
                    .ToListAsync();
                var existingImageDataBySku = existingVariants
                    .Where(v => !string.IsNullOrWhiteSpace(v.SKU) && v.ImageData != null && v.ImageData.Length > 0)
                    .GroupBy(v => v.SKU, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var existingImageDataByStyle = existingVariants
                    .Where(v => !string.IsNullOrWhiteSpace(v.Style) && v.ImageData != null && v.ImageData.Length > 0)
                    .GroupBy(v => v.Style, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                
                if (existingVariants.Any())
                {
                    _db.ProductVariants.RemoveRange(existingVariants);
                    await _db.SaveChangesAsync();
                }

                // Add new variants
                if (variants != null && variants.Count > 0)
                {
                    foreach (var variant in variants)
                    {
                        var imageData = variant.ImageData;
                        var imageMimeType = variant.ImageMimeType;

                        if ((imageData == null || imageData.Length == 0) &&
                            !string.IsNullOrWhiteSpace(variant.SKU) &&
                            existingImageDataBySku.TryGetValue(variant.SKU, out var existingBySku))
                        {
                            imageData = existingBySku.ImageData;
                            imageMimeType = existingBySku.ImageMimeType;
                        }

                        if ((imageData == null || imageData.Length == 0) &&
                            !string.IsNullOrWhiteSpace(variant.Style) &&
                            existingImageDataByStyle.TryGetValue(variant.Style, out var existingByStyle))
                        {
                            imageData = existingByStyle.ImageData;
                            imageMimeType = existingByStyle.ImageMimeType;
                        }

                        if ((imageData == null || imageData.Length == 0) && !string.IsNullOrWhiteSpace(variant.ImagePath))
                        {
                            var fileImageData = await TryReadPublicImageDataAsync(variant.ImagePath, HttpContext.RequestAborted);
                            if (fileImageData.Bytes != null)
                            {
                                imageData = fileImageData.Bytes;
                                imageMimeType = fileImageData.MimeType;
                            }
                        }

                        // Create a new instance to avoid tracking issues.
                        var newVariant = new DbProductVariant
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
                            ImageData = imageData,
                            ImageMimeType = imageMimeType,
                            Price = variant.Price,
                            Weight = variant.Weight,
                            Length = variant.Length,
                            Height = variant.Height,
                            Width = variant.Width
                        };
                        _db.ProductVariants.Add(newVariant);
                    }
                    await _db.SaveChangesAsync();
                    _logger.LogInformation($"Saved {variants.Count} variants for product {productId}");
                }
                else
                {
                    _logger.LogWarning($"No variants provided for product {productId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving variants for product {productId}");
                throw;
            }
        }

        private async Task<(byte[]? Bytes, string? MimeType)> TryReadPublicImageDataAsync(string? publicPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(publicPath) ||
                publicPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                publicPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                publicPath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return (null, null);
            }

            var relativePath = publicPath.TrimStart('~').TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            var webRoot = Path.GetFullPath(_env.WebRootPath);
            var fullPath = Path.GetFullPath(Path.Combine(webRoot, relativePath));

            if (!fullPath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            {
                return (null, null);
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, cancellationToken);
            return (bytes, NormalizeImageMimeType(null, fullPath));
        }

        private static string? NormalizeImageMimeType(string? contentType, string? fileNameOrPath)
        {
            if (!string.IsNullOrWhiteSpace(contentType) && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return contentType;
            }

            return Path.GetExtension(fileNameOrPath)?.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".avif" => "image/avif",
                ".svg" => "image/svg+xml",
                _ => null
            };
        }

        private async Task EnsureSellerNotificationsAsync(int sellerId, CancellationToken cancellationToken)
        {
            await _db.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Notifications
                    (
                        NotificationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        RecipientType NVARCHAR(20) NOT NULL,
                        RecipientId INT NOT NULL,
                        OrderId INT NULL,
                        Message NVARCHAR(500) NOT NULL,
                        IsRead BIT NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT(0),
                        CreatedAt DATETIME NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT(GETDATE()),
                        Category NVARCHAR(100) NULL
                    );
                END;

                IF COL_LENGTH(N'dbo.Notifications', N'Category') IS NULL
                    ALTER TABLE dbo.Notifications ADD Category NVARCHAR(100) NULL;
                """,
                cancellationToken);

            await _db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO dbo.Notifications
                (
                    RecipientType,
                    RecipientId,
                    OrderId,
                    Message,
                    IsRead,
                    CreatedAt,
                    Category
                )
                SELECT
                    N'Seller',
                    {0},
                    o.OrderID,
                    CONCAT(N'New order received. Order number: ', CONVERT(NVARCHAR(20), o.OrderID)),
                    0,
                    COALESCE(o.OrderDate, GETDATE()),
                    N'order'
                FROM dbo.Orders o
                WHERE o.seller_id = {0}
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.Notifications n
                      WHERE n.RecipientType = N'Seller'
                        AND n.RecipientId = {0}
                        AND n.OrderId = o.OrderID
                        AND ISNULL(n.Category, N'') = N'order'
                  );
                """,
                new object[] { sellerId },
                cancellationToken);
        }

        private async Task<List<SellerNotificationDto>> LoadSellerNotificationsAsync(int sellerId, int take, CancellationToken cancellationToken)
        {
            return await _db.Database.SqlQueryRaw<SellerNotificationDto>(
                    """
                    SELECT TOP ({1})
                        NotificationId,
                        OrderId,
                        Message,
                        IsRead,
                        CreatedAt,
                        ISNULL(Category, N'order') AS Category,
                        ISNULL(Category, N'order') AS Type,
                        CASE
                            WHEN ISNULL(Category, N'order') = N'order' THEN N'Order Update'
                            ELSE N'Notification'
                        END AS Title,
                        N'medium' AS Priority,
                        CAST(0 AS bit) AS ActionRequired,
                        N'in_app' AS DeliveryMode,
                        CASE
                            WHEN OrderId IS NOT NULL THEN N'order'
                            ELSE NULL
                        END AS LinkType,
                        CASE
                            WHEN OrderId IS NOT NULL THEN CONCAT(N'/Dashboard/OrderManagement?orderId=', CONVERT(NVARCHAR(20), OrderId))
                            ELSE N'#'
                        END AS LinkTarget
                    FROM dbo.Notifications
                    WHERE RecipientType = N'Seller'
                      AND RecipientId = {0}
                    ORDER BY CreatedAt DESC, NotificationId DESC;
                    """,
                    sellerId,
                    take)
                .ToListAsync(cancellationToken);
        }

        public sealed class SellerNotificationDto
        {
            public int NotificationId { get; set; }
            public int? OrderId { get; set; }
            public string Message { get; set; } = string.Empty;
            public bool IsRead { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Category { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Priority { get; set; } = "medium";
            public bool ActionRequired { get; set; }
            public string DeliveryMode { get; set; } = "in_app";
            public string? LinkType { get; set; }
            public string? LinkTarget { get; set; }
        }

        private void InvalidateProductCaches(int? sellerId = null)
        {
            _cache.Remove("products:all");
            _cache.Remove("products:men");
            _cache.Remove("products:women");
            if (sellerId.HasValue)
            {
                _cache.Remove($"seller:products:index:{sellerId.Value}");
            }
        }

        private async Task<int?> GetCurrentSellerIdAsync(CancellationToken cancellationToken = default)
        {
            var context = await _authUserContextService.GetCurrentAsync(cancellationToken);
            return context?.SellerId;
        }

        private async Task<List<DbProduct>> LoadSellerProductsWithImagesAsync(int sellerId)
        {
            var products = await _db.Products
                .Where(p => p.SellerId == sellerId)
                .ToListAsync();

            var productIds = products.Select(p => p.ProductId).ToList();
            if (productIds.Count == 0)
            {
                return products;
            }

            var variantImages = await _db.ProductVariants
                .Where(v => productIds.Contains(v.ProductId) && !string.IsNullOrEmpty(v.ImagePath))
                .GroupBy(v => v.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    ImagePath = g.OrderBy(v => v.Id).Select(v => v.ImagePath).FirstOrDefault()
                })
                .ToListAsync();

            var imageMap = variantImages.ToDictionary(x => x.ProductId, x => x.ImagePath);
            foreach (var product in products)
            {
                if (imageMap.TryGetValue(product.ProductId, out var imagePath) && !string.IsNullOrEmpty(imagePath))
                {
                    product.ImagePath = imagePath;
                }

                product.ImagePath = NormalizeImagePath(product.ImagePath);
            }

            return products;
        }

        private string NormalizeImagePath(string? path)
        {
            return _mediaPathService.NormalizePublicPath(path);
        }
    }
}
