using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyAspNetApp.Models
{
    [Table("challenge_participants")]
    public class DbChallengeParticipant
    {
        [Key]
        [Column("participant_id")]
        public int ParticipantId { get; set; }

        [Column("challenge_id")]
        public int ChallengeId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("consumer_id")]
        public int ConsumerId { get; set; }

        [Column("total_distance_km", TypeName = "decimal(10,2)")]
        public decimal? TotalDistanceKm { get; set; }

        [Column("total_activities")]
        public int? TotalActivities { get; set; }

        [Column("total_time_seconds")]
        public int? TotalTimeSeconds { get; set; }

        [Column("average_pace", TypeName = "decimal(10,2)")]
        public decimal? AveragePace { get; set; }

        [Column("rank")]
        public int? Rank { get; set; }

        [Column("joined_at")]
        public DateTime? JoinedAt { get; set; }

        [Column("last_activity_date")]
        public DateTime? LastActivityDate { get; set; }

        [Column("is_completed")]
        public bool? IsCompleted { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [Column("status")]
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";
    }
}
