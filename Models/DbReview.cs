using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyAspNetApp.Models
{
    [Table("Reviews", Schema = "dbo")]
    public class DbReview
    {
        [Key]
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;
        [Column(TypeName = "decimal(3,2)")]
        public decimal Rating { get; set; }
        public string ShortReview { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public bool VerifiedPurchase { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool Recommend { get; set; }
        public int? Comfort { get; set; }
        public int? Quality { get; set; }
        public int? SizeFit { get; set; }
        public int? WidthFit { get; set; }
        public int ProductId { get; set; }
        public string? SellerReply { get; set; }
        public DateTime? SellerReplyDate { get; set; }
    }

    [Table("ReviewImages", Schema = "dbo")]
    public class DbReviewImage
    {
        [Key]
        public int Id { get; set; }

        public int ReviewId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }
}
