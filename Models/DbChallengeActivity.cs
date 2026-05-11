using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyAspNetApp.Models
{
    [Table("challenge_activities")]
    public class DbChallengeActivity
    {
        [Key]
        [Column("activity_id")]
        public int ActivityId { get; set; }

        [Column("participant_id")]
        public int ParticipantId { get; set; }

        [Column("challenge_id")]
        public int ChallengeId { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("activity_date")]
        public DateTime ActivityDate { get; set; }

        [Column("distance_km", TypeName = "decimal(10,2)")]
        public decimal DistanceKm { get; set; }

        [Column("duration_seconds")]
        public int DurationSeconds { get; set; }

        [Column("average_pace", TypeName = "decimal(10,2)")]
        public decimal? AveragePace { get; set; }

        [Column("activity_type")]
        [Required]
        [MaxLength(50)]
        public string ActivityType { get; set; } = string.Empty;

        [Column("is_verified")]
        public bool? IsVerified { get; set; }

        [Column("verified_by")]
        public int? VerifiedBy { get; set; }

        [Column("verified_at")]
        public DateTime? VerifiedAt { get; set; }

        [Column("notes")]
        [MaxLength(500)]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("imageproof")]
        public byte[]? ImageProof { get; set; }
    }
}
