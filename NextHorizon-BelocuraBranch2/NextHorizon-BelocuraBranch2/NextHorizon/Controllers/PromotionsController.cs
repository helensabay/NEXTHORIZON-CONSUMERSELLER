using Microsoft.AspNetCore.Mvc;
using NextHorizon.Models;
using NextHorizon.Data;
using NextHorizon.Security;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NextHorizon.Services;

namespace NextHorizon.Controllers;

public class PromotionsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IAuthenticatedUserContextService _userContextService;
    private readonly ISellerNotificationService _sellerNotificationService;

    public PromotionsController(AppDbContext context, IAuthenticatedUserContextService userContextService, ISellerNotificationService sellerNotificationService)
    {
        _context = context;
        _userContextService = userContextService;
        _sellerNotificationService = sellerNotificationService;
    }

    // ── HELPER: Resolves the current logged-in seller's ID from the auth context ──
    private async Task<int> GetSellerIdAsync()
    {
        var userContext = await _userContextService.GetCurrentAsync(User);
        return userContext?.SellerId ?? 0;
    }

    // ── STEP 2 (View Promotions): Alternate entry point, renders Promotion.cshtml ──
    public async Task<IActionResult> Index()
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == 0) return Unauthorized();

        // Pulls all promotions for this seller from dbo.Promotions, newest first
        var promotions = await _context.Promotions
            .Where(p => p.SellerId == sellerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View("Promotion", promotions);
    }

    // ── STEP 2 (View Promotions): Main promotions list page ──
    // Fetches all promotions for the seller from dbo.Promotions
    // The view (Promotion.cshtml) then splits them into 4 tabs:
    //   Active | Pending | Inactive | Deleted
    public async Task<IActionResult> Promotion()
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == 0) return Unauthorized();

        var promotions = await _context.Promotions
            .Where(p => p.SellerId == sellerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(promotions);
    }

    // ── STEP 1 (Create/Edit Form): GET - Loads the AddPromotion form ──
    // If id is provided, loads existing promotion data into the form for editing
    // Also loads the seller's products into ViewBag.Products for the product selector
    [HttpGet]
    public async Task<IActionResult> AddPromotion(int? id)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == 0) return Unauthorized();

        // Load seller's active products for the product checkbox list
        ViewBag.Products = await _context.Products
            .Where(p => p.SellerId == sellerId)
            .ToListAsync();

        if (id.HasValue && id > 0)
        {
            // Edit mode: fetch existing promotion and map DB → ViewModel
            // MapToViewModel deserializes JSON columns back into typed lists
            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.PromotionId == id && p.SellerId == sellerId);

            if (promotion == null) return NotFound();

            return View(MapToViewModel(promotion));
        }

        // Create mode: blank form with default values (Status defaults to "Pending")
        return View(new PromotionViewModel());
    }

    // ── STEP 1 (Create/Edit Form): POST - Saves the promotion to dbo.Promotions ──
    // JSON serialization happens here before saving:
    //   - ReturnReasons        → List<string> → JSON string
    //   - ReturnConditions     → List<string> → JSON string
    //   - SelectedProductIds   → List<int>    → JSON string e.g. [35, 36]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPromotion(PromotionViewModel model)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == 0) return Unauthorized();

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Name))
        {
            ViewBag.Products = await _context.Products.Where(p => p.SellerId == sellerId).ToListAsync();
            return View(model);
        }

        try
        {
            // Serialize list fields to JSON for storage in nvarchar columns
            var returnReasonsJson = JsonSerializer.Serialize(model.ReturnReasons ?? new List<string>());
            var returnConditionsJson = JsonSerializer.Serialize(model.ReturnConditionRequirements ?? new List<string>());
            // SelectedProductIds stored as integer array e.g. [35] not ["35"]
            var selectedProductsJson = JsonSerializer.Serialize(model.SelectedProductIds ?? new List<int>());

            if (model.Id > 0)
            {
                // ── EDIT MODE: Update existing promotion in dbo.Promotions ──
                var dbPromotion = await _context.Promotions
                    .FirstOrDefaultAsync(p => p.PromotionId == model.Id && p.SellerId == sellerId);

                if (dbPromotion == null) return NotFound();

                MapToDb(model, dbPromotion, returnReasonsJson, returnConditionsJson, selectedProductsJson);
                dbPromotion.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // ── CREATE MODE: New promotion always starts as Pending ──
                // Status is forced to "Pending" regardless of what the form posts
                // Admin must approve before it becomes Active
                var dbPromotion = new DbPromotion
                {
                    SellerId = sellerId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Status = "Pending"
                };
                MapToDb(model, dbPromotion, returnReasonsJson, returnConditionsJson, selectedProductsJson);
                dbPromotion.Status = "Pending"; // always pending on create, never trust model status
                _context.Promotions.Add(dbPromotion);
            }

            await _context.SaveChangesAsync();

            await _sellerNotificationService.CreateAsync(new SellerNotification
            {
                RecipientType = "seller",
                RecipientId = sellerId.ToString(),
                SellerId = sellerId,
                Category = "system",
                Type = model.Id > 0 ? "system.promotion_updated" : "system.promotion_pending_review",
                Title = model.Id > 0 ? "Promotion Updated" : "Promotion Submitted",
                Message = model.Id > 0
                    ? $"Promotion {model.Name} was updated successfully."
                    : $"Promotion {model.Name} was submitted and is awaiting review.",
                Priority = model.Id > 0 ? "medium" : "high",
                DeliveryMode = "in_app",
                LinkType = "system_page",
                LinkTarget = "/Promotions/Promotion",
                ActionRequired = model.Id <= 0,
                DeduplicationKey = model.Id > 0 ? $"system.promotion_updated:{model.Id}" : null
            });

            return RedirectToAction(nameof(Promotion));
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            Console.WriteLine($"DEBUG: Error saving promotion: {msg}");
            ModelState.AddModelError("", $"Error saving promotion: {msg}");
            ViewBag.Products = await _context.Products.Where(p => p.SellerId == sellerId).ToListAsync();
            return View(model);
        }
    }

    // ── STEP 3 (Status Transition): Active → Inactive ──
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InactivatePromotion(int id)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == 0) return Unauthorized();

        var promotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.PromotionId == id && p.SellerId == sellerId);

        if (promotion == null) return NotFound();

        promotion.Status = "Inactive";
        promotion.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await CreatePromotionNotificationAsync(sellerId, "system.promotion_inactivated", "Promotion Inactivated", $"Promotion {promotion.Name} is now inactive.", "medium");

        return RedirectToAction(nameof(Promotion));
    }

    // ── STEP 3 (Status Transition): Pending/Inactive → Active ──
    // Once Active, the promotion is picked up by SellerController.Index
    // and applied to product prices on the Products page
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivatePromotion(int id)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == 0) return Unauthorized();

        var promotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.PromotionId == id && p.SellerId == sellerId);

        if (promotion == null) return NotFound();

        promotion.Status = "Active";
        promotion.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await CreatePromotionNotificationAsync(sellerId, "system.promotion_activated", "Promotion Activated", $"Promotion {promotion.Name} is now active.", "high");

        return RedirectToAction(nameof(Promotion));
    }

    // ── STEP 3 (Status Transition): Any → Deleted (soft delete) ──
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePromotion(int id)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == 0) return Unauthorized();

        var promotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.PromotionId == id && p.SellerId == sellerId);

        if (promotion == null) return NotFound();

        promotion.Status = "Deleted";
        promotion.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await CreatePromotionNotificationAsync(sellerId, "system.promotion_deleted", "Promotion Deleted", $"Promotion {promotion.Name} was deleted.", "medium");

        return RedirectToAction(nameof(Promotion));
    }

    // ── STEP 3 (Status Transition): Deleted → Active (restore) ──
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestorePromotion(int id)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == 0) return Unauthorized();

        var promotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.PromotionId == id && p.SellerId == sellerId);

        if (promotion == null) return NotFound();

        promotion.Status = "Active";
        promotion.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await CreatePromotionNotificationAsync(sellerId, "system.promotion_restored", "Promotion Restored", $"Promotion {promotion.Name} was restored and reactivated.", "medium");

        return RedirectToAction(nameof(Promotion));
    }

    // ── MAPPER: ViewModel → DbPromotion (used for both create and edit) ──
    // Note: Status is set here from model but overridden to "Pending" on create
    // TotalDiscountPercent / TotalDiscountFix: only one is used depending on Type
    // UntilPromotionLast: if true, UsageLimit is ignored and shown as "Unlimited"
    private static void MapToDb(PromotionViewModel model, DbPromotion db,
        string returnReasonsJson, string returnConditionsJson, string selectedProductsJson)
    {
        db.Name = model.Name;
        db.Type = model.Type;
        db.BannerSize = model.BannerSize;
        db.TotalDiscountPercent = model.TotalDiscountPercent;   // used when Type = "Percentage Discount"
        db.TotalDiscountFix = model.TotalDiscountFix;           // used when Type = "Fix Amount"
        db.UsageLimit = model.UsageLimit;
        db.UntilPromotionLast = model.UntilPromotionLast;       // true = unlimited usage
        db.BuyQuantity = model.BuyQuantity;
        db.TakeQuantity = model.TakeQuantity;
        db.FreeItemRequirement = model.FreeItemRequirement;
        db.ReturnWindowDays = model.ReturnWindowDays;
        db.ReturnReasonsJson = returnReasonsJson;                // JSON: ["Damaged", "Incomplete"]
        db.ReturnConditionRequirementsJson = returnConditionsJson; // JSON: ["Unused", "Tags"]
        db.MinimumRequirementType = model.MinimumRequirementType;
        db.MinimumPurchaseAmount = model.MinimumPurchaseAmount;
        db.Status = model.Status;
        db.SelectedProductIdsJson = selectedProductsJson;        // JSON: [35, 36] — integer array
    }

    // ── MAPPER: DbPromotion → ViewModel (used when loading edit form) ──
    // Deserializes all JSON columns back into typed C# lists
    // ── PRODUCT SEARCH: Called via AJAX from AddPromotion form ──
    // Executes sp_SearchSellerProducts joining Products + ProductVariants
    // Searches by ProductName, ProductId, or SKU
    [HttpGet]
    public async Task<IActionResult> SearchProducts(string? q)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == 0) return Unauthorized();

        var connStr = _context.Database.GetConnectionString();
        var results = new List<object>();

        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connStr);
        using var cmd = new Microsoft.Data.SqlClient.SqlCommand("sp_SearchSellerProducts", conn);
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@SellerId", sellerId);
        cmd.Parameters.AddWithValue("@Search", (object?)q ?? DBNull.Value);

        await conn.OpenAsync();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new
            {
                productId = reader["ProductId"],
                productName = reader["ProductName"].ToString(),
                price = reader["Price"],
                category = reader["Category"]?.ToString(),
                sku = reader["SKU"]?.ToString(),
                imagePath = reader["ImagePath"]?.ToString()
            });
        }

        return Json(results);
    }

    private static PromotionViewModel MapToViewModel(DbPromotion db) => new()
    {
        Id = db.PromotionId,
        Name = db.Name,
        Type = db.Type,
        BannerSize = db.BannerSize,
        TotalDiscountPercent = db.TotalDiscountPercent,
        TotalDiscountFix = db.TotalDiscountFix,
        UsageLimit = db.UsageLimit,
        UntilPromotionLast = db.UntilPromotionLast,
        BuyQuantity = db.BuyQuantity,
        TakeQuantity = db.TakeQuantity,
        FreeItemRequirement = db.FreeItemRequirement,
        ReturnWindowDays = db.ReturnWindowDays,
        ReturnReasons = JsonSerializer.Deserialize<List<string>>(db.ReturnReasonsJson) ?? new(),
        ReturnConditionRequirements = JsonSerializer.Deserialize<List<string>>(db.ReturnConditionRequirementsJson) ?? new(),
        MinimumRequirementType = db.MinimumRequirementType,
        MinimumPurchaseAmount = db.MinimumPurchaseAmount,
        Status = db.Status,
        SelectedProductIds = JsonSerializer.Deserialize<List<int>>(db.SelectedProductIdsJson) ?? new()
    };

    private Task CreatePromotionNotificationAsync(int sellerId, string type, string title, string message, string priority)
    {
        return _sellerNotificationService.CreateAsync(new SellerNotification
        {
            RecipientType = "seller",
            RecipientId = sellerId.ToString(),
            SellerId = sellerId,
            Category = "system",
            Type = type,
            Title = title,
            Message = message,
            Priority = priority,
            DeliveryMode = "in_app",
            LinkType = "system_page",
            LinkTarget = "/Promotions/Promotion",
            ActionRequired = false
        });
    }
}
