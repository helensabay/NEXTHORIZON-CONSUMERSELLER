namespace MyAspNetApp.Models.ViewModels;

public sealed class ProfileViewPageViewModel
{
    public string DisplayName { get; set; } = "User";
    public string Username { get; set; } = "USER";
    public string Motto { get; set; } = "No motto set yet.";
    public string? AvatarUrl { get; set; }
    public string GlobalRankText { get; set; } = "Unranked";
    public decimal TodayDistanceKm { get; set; }
    public int TodayTimeSeconds { get; set; }
    public int TodaySteps { get; set; }
    public List<ProfileActivityCardViewModel> RecentActivities { get; set; } = new();
}

public sealed class ProfileActivityCardViewModel
{
    public string? ImageUrl { get; set; }
    public decimal DistanceKm { get; set; }
    public int DurationSeconds { get; set; }
    public int EstimatedSteps { get; set; }
    public string RankText { get; set; } = "Unranked";
    public DateTime ActivityDate { get; set; }
}

public sealed class UpdateProfilePageViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Motto { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? Birthday { get; set; }
    public string BirthdayText => Birthday?.ToString("yyyy-MM-dd") ?? string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
