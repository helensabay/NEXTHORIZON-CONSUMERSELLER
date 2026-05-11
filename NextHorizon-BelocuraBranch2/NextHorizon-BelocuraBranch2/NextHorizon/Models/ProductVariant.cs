using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextHorizon.Models;

[Table("ProductVariants")] // Tells C# to look at the SQL table 'ProductVariants'
public class ProductVariant
{
    [Key]
    public int VariantId { get; set; }
    
    public int ProductId { get; set; }
    
    public string SKU { get; set; } = string.Empty;
    
    public string Size { get; set; } = string.Empty;
    
    [Column("Style")] // Tells C# that 'Style' in SQL maps to 'Style' here
    public string Style { get; set; } = string.Empty; 

    public int Quantity { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Price { get; set; }
}