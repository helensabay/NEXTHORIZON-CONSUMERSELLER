using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextHorizon.Models;

[Table("Notifications")]
public class SellerNotification
{
    [Key]
    public int NotificationId { get; set; }

    [Required]
    [MaxLength(32)]
    public string RecipientType { get; set; } = "seller";

    [Required]
    [MaxLength(64)]
    public string RecipientId { get; set; } = string.Empty;

    [NotMapped]
    public int? SellerId { get; set; }

    public int? OrderId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Category { get; set; } = string.Empty;

    [NotMapped]
    [Required]
    [MaxLength(128)]
    public string Type { get; set; } = string.Empty;

    [NotMapped]
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    [NotMapped]
    public DateTime? ReadAt { get; set; }

    [NotMapped]
    [Required]
    [MaxLength(32)]
    public string Priority { get; set; } = "medium";

    [NotMapped]
    [Required]
    [MaxLength(128)]
    public string DeliveryMode { get; set; } = "in_app";

    [NotMapped]
    public bool ActionRequired { get; set; }

    [NotMapped]
    [MaxLength(64)]
    public string? LinkType { get; set; }

    [NotMapped]
    [MaxLength(400)]
    public string? LinkTarget { get; set; }

    [NotMapped]
    [MaxLength(256)]
    public string? DeduplicationKey { get; set; }

    [NotMapped]
    [Column(TypeName = "nvarchar(max)")]
    public string? MetadataJson { get; set; }
}
