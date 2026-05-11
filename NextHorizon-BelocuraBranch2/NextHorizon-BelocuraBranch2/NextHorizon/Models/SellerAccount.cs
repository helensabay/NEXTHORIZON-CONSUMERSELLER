using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextHorizon.Models;

[Table("Sellers")]
public class SellerAccount
{
    [Key]
    [Column("seller_id")]
    public int SellerId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("business_type")]
    public string? BusinessType { get; set; }

    [Column("business_name")]
    public string? BusinessName { get; set; }

    [Column("business_email")]
    public string? BusinessEmail { get; set; }

    [Column("business_phone")]
    public string? BusinessPhone { get; set; }

    [Column("tax_id")]
    public string? TaxId { get; set; }

    [Column("business_address")]
    public string? BusinessAddress { get; set; }

    [Column("logo_path")]
    public string? LogoPath { get; set; }

    [Column("document_path")]
    public string? DocumentPath { get; set; }

    [Column("seller_status")]
    public string SellerStatus { get; set; } = "Pending";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
