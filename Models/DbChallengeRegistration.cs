using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyAspNetApp.Models
{
    [Table("challengeregistration", Schema = "dbo")]
    public class DbChallengeRegistration
    {
        [Key]
        [Column("registration_id")]
        public int RegistrationId { get; set; }

        [Column("challenge_id")]
        public int ChallengeId { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [MaxLength(120)]
        [Column("city")]
        public string? City { get; set; }

        [MaxLength(500)]
        [Column("notes")]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("status")]
        public string Status { get; set; } = "Pending";

        [Column("submitted_at")]
        public DateTime SubmittedAt { get; set; }

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }

        [Column("approval_notified_at")]
        public DateTime? ApprovalNotifiedAt { get; set; }

        [Column("reviewed_by")]
        public int? ReviewedBy { get; set; }

        [MaxLength(500)]
        [Column("admin_remarks")]
        public string? AdminRemarks { get; set; }
    }
}
