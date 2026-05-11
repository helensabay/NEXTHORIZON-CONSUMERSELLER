using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextHorizon.Models
{
    [Table("SizeGuides")]
    public class DbSizeGuide
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string? Title { get; set; }

        public string? MeasurementUnit { get; set; }

        public string? Category { get; set; }

        public string? FitTips { get; set; }

        public string? HowToMeasure { get; set; }

        public string? AdditionalNotes { get; set; }

        // Holds the table structure and values as JSON.
        public string? TableJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ProductId))]
        public DbProduct? Product { get; set; }

        public List<DbSizeGuideImage> Images { get; set; } = new();
    }

    [Table("SizeGuideImages")]
    public class DbSizeGuideImage
    {
        [Key]
        public int Id { get; set; }

        public int SizeGuideId { get; set; }

        public string ImagePath { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        [ForeignKey(nameof(SizeGuideId))]
        public DbSizeGuide? SizeGuide { get; set; }
    }
}