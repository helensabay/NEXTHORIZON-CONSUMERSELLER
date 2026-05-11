using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace MyAspNetApp.Models
{
    [Table("Promotions")]
    public class DbPromotion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty;

        public string? BannerSize { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalDiscountPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalDiscountFix { get; set; }

        public int? UsageLimit { get; set; }
        public bool UntilPromotionLast { get; set; }
        public int? BuyQuantity { get; set; }
        public int? TakeQuantity { get; set; }
        public string? FreeItemRequirement { get; set; }
        public int? ReturnWindowDays { get; set; }

        public string? ReturnReasonsJson { get; set; } = "[]";

        public string? ReturnConditionRequirementsJson { get; set; } = "[]";

        public string? MinimumRequirementType { get; set; } = "None";

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinimumPurchaseAmount { get; set; }

        public string? Status { get; set; } = "Pending";

        public string? SelectedProductIdsJson { get; set; } = "[]";

        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

    }
}
