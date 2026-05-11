using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace MyAspNetApp.Models.ViewModels
{
    public class UploadChallengeActivityViewModel
    {
        [Required]
        public int ParticipantId { get; set; }

        [Required]
        public int ChallengeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ActivityType { get; set; } = string.Empty;

        [Required]
        public DateTime ActivityDate { get; set; }

        [Range(0.01, 1000)]
        public decimal DistanceKm { get; set; }

        [Range(0, 23)]
        public int Hours { get; set; }

        [Range(0, 59)]
        public int Minutes { get; set; }

        [Range(0, 59)]
        public int Seconds { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public IFormFile? ProofImage { get; set; }
    }
}
