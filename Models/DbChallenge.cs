using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyAspNetApp.Models
{
    [Table("challenges", Schema = "dbo")]
    public class DbChallenge
    {
        [Key]
        [Column("challenge_id")]
        public int ChallengeId { get; set; }

        [Required]
        [Column("title")]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column("rules")]
        public string Rules { get; set; } = string.Empty;

        [Required]
        [Column("prizes")]
        public string Prizes { get; set; } = string.Empty;

        [Column("goal_km", TypeName = "decimal(10,2)")]
        public decimal GoalKm { get; set; }

        [Required]
        [Column("activity_type")]
        [MaxLength(50)]
        public string ActivityType { get; set; } = string.Empty;

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("end_date")]
        public DateTime EndDate { get; set; }

        [Column("status")]
        [MaxLength(20)]
        public string? Status { get; set; }

        [Column("banner_image")]
        public byte[]? BannerImage { get; set; }

        [Column("banner_image_name")]
        [MaxLength(255)]
        public string? BannerImageName { get; set; }

        [Column("banner_image_content_type")]
        [MaxLength(100)]
        public string? BannerImageContentType { get; set; }

        [Column("created_by")]
        public int CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        [Column("total_participants")]
        public int? TotalParticipants { get; set; }

        [Column("total_completed")]
        public int? TotalCompleted { get; set; }
    }
}
