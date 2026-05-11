using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MyAspNetApp.Models
{
    public class DbLeaderboardRecord
    {
        public int Id { get; set; }

        public int UploadId { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(120)]
        public string AthleteName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        [MaxLength(500)]
        public string? CoverImageUrl { get; set; }

        [Precision(8, 2)]
        public decimal DistanceKm { get; set; }

        public int DurationSeconds { get; set; }

        [MaxLength(50)]
        public string Scope { get; set; } = "National";

        [MaxLength(80)]
        public string CategoryLabel { get; set; } = "Current Season";

        public int RankChange { get; set; }

        public bool IsVerified { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ActivityDate { get; set; }
    }
}
