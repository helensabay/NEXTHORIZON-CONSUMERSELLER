using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextHorizon.Models
{
    [Table("Promotions")]
    public class DbPromotion
    {
        [Key]
        [Column("Id")]
        public int PromotionId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "Percentage Discount";

        [MaxLength(20)]
        public string BannerSize { get; set; } = "300x250";

        public decimal? TotalDiscountPercent { get; set; }

        public decimal? TotalDiscountFix { get; set; }

        public int UsageLimit { get; set; } = 100;

        public bool UntilPromotionLast { get; set; } = false;

        public int BuyQuantity { get; set; } = 2;

        public int TakeQuantity { get; set; } = 1;

        [MaxLength(50)]
        public string FreeItemRequirement { get; set; } = "ExactProduct";

        public int ReturnWindowDays { get; set; } = 30;

        [Column("ReturnReasonsJson", TypeName = "nvarchar(max)")]
        public string ReturnReasonsJson { get; set; } = "[]";

        [Column("ReturnConditionRequirementsJson", TypeName = "nvarchar(max)")]
        public string ReturnConditionRequirementsJson { get; set; } = "[]";

        [MaxLength(50)]
        public string MinimumRequirementType { get; set; } = "None";

        public decimal MinimumPurchaseAmount { get; set; } = 0;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Active";

        [Column("SelectedProductIdsJson", TypeName = "nvarchar(max)")]
        public string SelectedProductIdsJson { get; set; } = "[]";

        [Column("user_id")]
        public int SellerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("rejection_reason")]
        public string? RejectionReason { get; set; }

        [Column("processed_by")]
        public string? ProcessedBy { get; set; }
    }
}
