using System;
using System.ComponentModel.DataAnnotations;

namespace NextHorizon.Models
{
using System.ComponentModel.DataAnnotations.Schema;

[Table("FAQs")]
public class Faq
    {
        [Key]
        public int FaqID { get; set; }

        [Required]
        [MaxLength(500)]
        public string Question { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Answer { get; set; }

        [Required]
        [MaxLength(100)]
        public string Category { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } // "Active" / "Inactive"

        [Column("user_id")]
        public int? UserId { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.Now;

        public DateTime? LastUpdated { get; set; }

        [Required]
        [MaxLength(50)]
        public string UserType { get; set; } // "Admin"
    }
}