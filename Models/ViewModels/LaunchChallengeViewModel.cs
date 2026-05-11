using System.ComponentModel.DataAnnotations;

namespace MyAspNetApp.Models.ViewModels;

public class LaunchChallengeViewModel
{
    [Required(ErrorMessage = "Challenge cover image is required.")]
    public IFormFile? ChallengeCoverImage { get; set; }

    [Required(ErrorMessage = "Challenge name is required.")]
    [StringLength(200)]
    public string ChallengeName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Start date is required.")]
    [DataType(DataType.Date)]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "End date is required.")]
    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }

    [Required(ErrorMessage = "Activity type is required.")]
    public string ActivityType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Goal in kilometers is required.")]
    [Range(0.01, 100000, ErrorMessage = "Goal KM must be greater than 0.")]
    public decimal? GoalKm { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rules are required.")]
    [StringLength(4000)]
    public string Rules { get; set; } = string.Empty;

    [Required(ErrorMessage = "Prizes are required.")]
    [StringLength(4000)]
    public string Prizes { get; set; } = string.Empty;
}
