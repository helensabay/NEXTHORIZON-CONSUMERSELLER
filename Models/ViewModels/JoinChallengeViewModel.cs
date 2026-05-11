using System.ComponentModel.DataAnnotations;

namespace MyAspNetApp.Models.ViewModels
{
    public class JoinChallengeViewModel
    {
        [Required]
        public int ChallengeId { get; set; }

        [Required]
        [StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [StringLength(120)]
        public string? City { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
