namespace MyAspNetApp.Models
{
    public class ChallengesPageViewModel
    {
        public string SeasonLabel { get; set; } = string.Empty;
        public int ActiveCount { get; set; }
        public int TotalParticipants { get; set; }
        public decimal TotalGoalKm { get; set; }
        public int DaysUntilNextDrop { get; set; }
        public ChallengeCardViewModel? FeaturedChallenge { get; set; }
        public List<ChallengeCardViewModel> UpcomingChallenges { get; set; } = new();
        public List<ChallengeCardViewModel> CompletedChallenges { get; set; } = new();
        public List<ChallengeCardViewModel> TopChallenges { get; set; } = new();
        public List<VoucherViewModel>? Vouchers { get; set; }
        public string? TotalSavedDisplay { get; set; }
        public List<ChallengeParticipationCardViewModel> MyActivities { get; set; } = new();
        public List<ChallengeUploadedActivityViewModel> UploadedActivities { get; set; } = new();
        public int MyActivitiesCount { get; set; }
        public int ApprovedActivitiesCount { get; set; }
        public int PendingActivitiesCount { get; set; }
        public MyAspNetApp.Models.ViewModels.JoinChallengeViewModel JoinChallengePrefill { get; set; } = new();
        public MyAspNetApp.Models.ViewModels.UploadChallengeActivityViewModel UploadActivityPrefill { get; set; } = new();
    }

    public class ChallengeCardViewModel
    {
        public int ChallengeId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Rules { get; set; } = string.Empty;
        public string Prizes { get; set; } = string.Empty;
        public decimal GoalKm { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalParticipants { get; set; }
        public int TotalCompleted { get; set; }
        public double CompletionPercent { get; set; }
        public int DurationDays { get; set; }
        public string DifficultyLabel { get; set; } = string.Empty;
        public string DifficultyCssClass { get; set; } = string.Empty;
        public bool HasBannerImage { get; set; }
        public List<ChallengeLeaderboardEntryViewModel> LeaderboardEntries { get; set; } = new();
    }

    public class ChallengeLeaderboardEntryViewModel
    {
        public int Rank { get; set; }
        public int UserId { get; set; }
        public string AthleteName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string DistanceDisplay { get; set; } = string.Empty;
        public string DurationDisplay { get; set; } = string.Empty;
    }

    public class ChallengeParticipationCardViewModel
    {
        public int ParticipantId { get; set; }
        public int ChallengeId { get; set; }
        public string ChallengeTitle { get; set; } = string.Empty;
        public string ChallengeDescription { get; set; } = string.Empty;
        public string ActivityType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public bool CanUpload { get; set; }
        public bool IsExpired { get; set; }
        public decimal GoalKm { get; set; }
        public string GoalDisplay { get; set; } = string.Empty;
        public int TotalActivities { get; set; }
        public decimal TotalDistanceKm { get; set; }
        public string TotalDistanceDisplay { get; set; } = string.Empty;
        public int TotalTimeSeconds { get; set; }
        public string TotalTimeDisplay { get; set; } = string.Empty;
        public string LastActivityDisplay { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class ChallengeUploadedActivityViewModel
    {
        public int ActivityId { get; set; }
        public string ChallengeTitle { get; set; } = string.Empty;
        public string ActivityType { get; set; } = string.Empty;
        public string VerificationLabel { get; set; } = string.Empty;
        public string VerificationCssClass { get; set; } = string.Empty;
        public string ActivityDateDisplay { get; set; } = string.Empty;
        public string DistanceDisplay { get; set; } = string.Empty;
        public string DurationDisplay { get; set; } = string.Empty;
        public string AveragePaceDisplay { get; set; } = string.Empty;
        public string ProofImageUrl { get; set; } = string.Empty;
        public bool HasProofImage { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
