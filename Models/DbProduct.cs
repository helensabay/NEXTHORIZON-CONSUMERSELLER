using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyAspNetApp.Models
{
    [Table("Products")]
    public class DbProduct
    {
        [Key]
        public int ProductId { get; set; }

        [Required]
        public string ProductName { get; set; } = string.Empty;

        // price may also be stored per-variant; do not map if column is gone
        [NotMapped]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        // discount was originally a product‑level column but may be removed;
        // do not map it directly so missing column won't crash queries.
        [NotMapped]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Discount { get; set; }

        public string? Category { get; set; }

        public string? Details { get; set; }

        // images are now stored on variants; product table column has been removed
        [NotMapped]
        public string? ImagePath { get; set; }

        public string? Status { get; set; } = "active";

        public string? Brand { get; set; }

        // stock & sizes moved to variants, leave here unmapped to avoid invalid SQL
        [NotMapped]
        public int Stock { get; set; } = 0;

        [NotMapped]
        public string? Sizes { get; set; }

        // new gender column: Men, Women, Unisex, etc.
        [Required]
        public string Gender { get; set; } = string.Empty;

        [Column("seller_id")]
        public int SellerId { get; set; }

        // SKU and dimensions (stored on ProductVariants; keep here for backward compatibility but not mapped to Products table)
        [NotMapped]
        public string? SKU { get; set; }

        [NotMapped]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Weight { get; set; }

        [NotMapped]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Length { get; set; }

        [NotMapped]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Height { get; set; }

        [NotMapped]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Width { get; set; }

        [NotMapped]
        public int? ReviewCount { get; set; }

        [NotMapped]
        public double? AverageRating { get; set; }

        [NotMapped]
        public string? RejectionReason { get; set; }
    }

    [Table("ProductColorImages")]
    public class DbProductColorImage
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }

        [Required]
        public string ColorName { get; set; } = string.Empty;

        [Required]
        public string ImagePath { get; set; } = string.Empty;
    }

    [Table("ProductVariants")]
    public class DbProductVariant
    {
        [Key]
        [Column("VariantId")]
        public int Id { get; set; }

        public int ProductId { get; set; }

        [Required]
        public string SKU { get; set; } = string.Empty;

        [Required]
        public string Size { get; set; } = string.Empty;

        [Required]
        public string Style { get; set; } = string.Empty;

        public int Quantity { get; set; }

        [Required]
        public string Availability { get; set; } = "In Stock";

        [Column("imagePath")]
        public string? ImagePath { get; set; }

        [Column("ImageData", TypeName = "varbinary(max)")]
        public byte[]? ImageData { get; set; }

        [Column("ImageMimeType", TypeName = "nvarchar(200)")]
        public string? ImageMimeType { get; set; }
        // price specific to this variant; if null, use parent product price
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Price { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Length { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Height { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Width { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Weight { get; set; }

        // Compatibility aliases for existing app code.
        [NotMapped]
        public string ColorName
        {
            get => Style;
            set => Style = value ?? string.Empty;
        }

        [NotMapped]
        public string? Sizes
        {
            get => Size;
            set => Size = value ?? string.Empty;
        }

        [NotMapped]
        public int Stock
        {
            get => Quantity;
            set => Quantity = value;
        }

        [ForeignKey(nameof(ProductId))]
        [System.Text.Json.Serialization.JsonIgnore]
        public DbProduct? Product { get; set; }
    }
}
