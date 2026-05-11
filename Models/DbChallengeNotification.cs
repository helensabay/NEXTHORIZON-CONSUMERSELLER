using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyAspNetApp.Models
{
    [Table("challengenotification", Schema = "dbo")]
    public class DbChallengeNotification
    {
        [Key]
        [Column("notification_id")]
        public int NotificationId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("registration_id")]
        public int? RegistrationId { get; set; }

        [Required]
        [MaxLength(160)]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(400)]
        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        [Column("notification_type")]
        public string NotificationType { get; set; } = "general";

        [Column("is_read")]
        public bool IsRead { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("read_at")]
        public DateTime? ReadAt { get; set; }
    }
}
