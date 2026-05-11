using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextHorizon.Models;

[Table("returns")]
public class ReturnRequest
{
    [Key]
    [Column("ReturnId")]
    public int ReturnId { get; set; }

    [Column("OrderId")]
    public int OrderId { get; set; }

    [Column("UserId")]
    public int UserId { get; set; }

    [Column("SellerId")]
    public int? SellerId { get; set; }

    [Required]
    [StringLength(150)]
    [Column("Reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("Message")]
    public string? Message { get; set; }

    [Column("FileName")]
    [StringLength(260)]
    public string? FileName { get; set; }

    [Column("ContentType")]
    [StringLength(100)]
    public string? ContentType { get; set; }

    [Column("ImageData", TypeName = "varbinary(max)")]
    public byte[]? ImageData { get; set; }

    [Column("Status")]
    [StringLength(50)]
    public string Status { get; set; } = "Return Requested";

    [Column("SellerDecisionReason")]
    [StringLength(150)]
    public string? SellerDecisionReason { get; set; }

    [Column("SellerDecisionNote")]
    public string? SellerDecisionNote { get; set; }

    [Column("ReviewedAt")]
    public DateTime? ReviewedAt { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }

    [NotMapped]
    public string BuyerName { get; set; } = string.Empty;

    [NotMapped]
    public DateTime? OrderDate { get; set; }

    [NotMapped]
    public string ImageUrl => HasImage ? $"/Dashboard/ReturnRequestImage?returnId={ReturnId}" : string.Empty;

    [NotMapped]
    public bool HasImage => ImageData is { Length: > 0 };
}
