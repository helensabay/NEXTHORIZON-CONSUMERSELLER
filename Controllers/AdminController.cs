using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyAspNetApp.Data;
using MyAspNetApp.Models;
using MyAspNetApp.Models.ViewModels;

namespace MyAspNetApp.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public AdminController(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<IActionResult> ProductApprovals()
        {
            var products = await _db.Products
                .AsNoTracking()
                .OrderByDescending(p => p.ProductId)
                .ToListAsync();

            var productIds = products.Select(p => p.ProductId).ToList();
            var variantImages = await _db.ProductVariants
                .AsNoTracking()
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
                if (string.IsNullOrWhiteSpace(product.ImagePath) &&
                    imageMap.TryGetValue(product.ProductId, out var imagePath) &&
                    !string.IsNullOrWhiteSpace(imagePath))
                {
                    product.ImagePath = imagePath;
                }
            }

            var model = new ProductApprovalDashboardViewModel
            {
                PendingProducts = products.Where(p => string.Equals(p.Status, "pending", StringComparison.OrdinalIgnoreCase)).ToList(),
                ApprovedProducts = products.Where(p => string.Equals(p.Status, "approved", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Status, "active", StringComparison.OrdinalIgnoreCase)).ToList(),
                RejectedProducts = products.Where(p => string.Equals(p.Status, "rejected", StringComparison.OrdinalIgnoreCase)).ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> PromotionApprovals()
        {
            var promotions = await _db.Promotions
                .AsNoTracking()
                .OrderByDescending(p => p.Id)
                .ToListAsync();
            var model = new PromotionApprovalDashboardViewModel
            {
                PendingPromotions = promotions.Where(p => string.Equals(p.Status, "Pending", StringComparison.OrdinalIgnoreCase)).Select(ToViewModel).ToList(),
                ApprovedPromotions = promotions.Where(p => string.Equals(p.Status, "Approved", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Status, "active", StringComparison.OrdinalIgnoreCase)).Select(ToViewModel).ToList(),
                RejectedPromotions = promotions.Where(p => string.Equals(p.Status, "Rejected", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Status, "inactive", StringComparison.OrdinalIgnoreCase)).Select(ToViewModel).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveProduct(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product != null)
            {
                product.Status = "approved";
                await _db.SaveChangesAsync();
                InvalidateProductCaches();
                TempData["AdminSuccessMessage"] = "Product approved.";
            }

            return RedirectToAction(nameof(ProductApprovals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectProduct(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product != null)
            {
                product.Status = "rejected";
                await _db.SaveChangesAsync();
                InvalidateProductCaches();
                TempData["AdminSuccessMessage"] = "Product rejected.";
            }

            return RedirectToAction(nameof(ProductApprovals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePromotion(int id)
        {
            var promotion = await _db.Promotions.FindAsync(id);
            if (promotion != null)
            {
                promotion.Status = "active";
                promotion.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                TempData["AdminSuccessMessage"] = "Promotion approved.";
            }

            return RedirectToAction(nameof(PromotionApprovals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPromotion(int id)
        {
            var promotion = await _db.Promotions.FindAsync(id);
            if (promotion != null)
            {
                promotion.Status = "inactive";
                promotion.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                TempData["AdminSuccessMessage"] = "Promotion rejected.";
            }

            return RedirectToAction(nameof(PromotionApprovals));
        }

        private static PromotionViewModel ToViewModel(DbPromotion entity)
        {
            return new PromotionViewModel
            {
                Id = entity.Id,
                Name = entity.Name,
                Type = entity.Type,
                BannerSize = entity.BannerSize ?? "300x250",
                TotalDiscountPercent = entity.TotalDiscountPercent ?? 0m,
                TotalDiscountFix = entity.TotalDiscountFix ?? 0m,
                UsageLimit = entity.UsageLimit ?? 0,
                UntilPromotionLast = entity.UntilPromotionLast,
                BuyQuantity = entity.BuyQuantity ?? 0,
                TakeQuantity = entity.TakeQuantity ?? 0,
                FreeItemRequirement = entity.FreeItemRequirement ?? "ExactProduct",
                ReturnWindowDays = entity.ReturnWindowDays ?? 0,
                ReturnReasons = PromotionSerialization.Deserialize(entity.ReturnReasonsJson ?? "[]"),
                ReturnConditionRequirements = PromotionSerialization.Deserialize(entity.ReturnConditionRequirementsJson ?? "[]"),
                MinimumRequirementType = entity.MinimumRequirementType ?? "None",
                MinimumPurchaseAmount = entity.MinimumPurchaseAmount ?? 0m,
                Status = entity.Status,
                SelectedProductIds = PromotionSerialization.Deserialize(entity.SelectedProductIdsJson ?? "[]")
            };
        }

        private void InvalidateProductCaches()
        {
            _cache.Remove("products:all");
            _cache.Remove("products:men");
            _cache.Remove("products:women");
            _cache.Remove("seller:products:index");
        }
    }
}
