using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using MyAspNetApp.Models;

namespace MyAspNetApp.Controllers
{
    public class PromotionsController : Controller
    {
        private readonly AppDbContext _db;

        public PromotionsController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Promotion()
        {
            var promotions = await _db.Promotions
                .AsNoTracking()
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            return View(promotions.Select(ToViewModel).ToList());
        }

        [HttpGet]
        public IActionResult AddPromotion(int? id)
        {
            if (id.HasValue && id > 0)
            {
                var existing = _db.Promotions.Find(id.Value);
                if (existing != null)
                {
                    return View(ToViewModel(existing));
                }
            }

            return View(new PromotionViewModel
            {
                Status = "pending"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPromotion(PromotionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Id > 0)
            {
                var existing = await _db.Promotions.FindAsync(model.Id);
                if (existing != null)
                {
                    PopulateEntity(existing, model);
                    existing.Status = NormalizeStatus(model.Status, existing.Status);
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                var entity = new DbPromotion();
                PopulateEntity(entity, model);
                entity.Status = NormalizeStatus(model.Status, "pending");
                entity.CreatedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
                await _db.Promotions.AddAsync(entity);
            }

            await _db.SaveChangesAsync();
            TempData["PromotionSuccessMessage"] = "Promotion saved successfully.";
            return RedirectToAction(nameof(Promotion));
        }

        private static void PopulateEntity(DbPromotion entity, PromotionViewModel model)
        {
            entity.Name = model.Name;
            entity.Type = model.Type;
            entity.BannerSize = model.BannerSize;
            entity.TotalDiscountPercent = model.TotalDiscountPercent;
            entity.TotalDiscountFix = model.TotalDiscountFix;
            entity.UsageLimit = model.UsageLimit;
            entity.UntilPromotionLast = model.UntilPromotionLast;
            entity.BuyQuantity = model.BuyQuantity;
            entity.TakeQuantity = model.TakeQuantity;
            entity.FreeItemRequirement = model.FreeItemRequirement;
            entity.ReturnWindowDays = model.ReturnWindowDays;
            entity.ReturnReasonsJson = PromotionSerialization.Serialize(model.ReturnReasons);
            entity.ReturnConditionRequirementsJson = PromotionSerialization.Serialize(model.ReturnConditionRequirements);
            entity.MinimumRequirementType = model.MinimumRequirementType;
            entity.MinimumPurchaseAmount = model.MinimumPurchaseAmount;
            entity.SelectedProductIdsJson = PromotionSerialization.Serialize(model.SelectedProductIds);
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

        private static string NormalizeStatus(string? status, string fallback)
        {
            return status?.Trim().ToLowerInvariant() switch
            {
                "active" => "active",
                "inactive" => "inactive",
                "pending" => "pending",
                _ => fallback
            };
        }
    }
}
