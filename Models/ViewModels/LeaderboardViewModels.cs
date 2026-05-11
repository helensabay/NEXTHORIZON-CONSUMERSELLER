namespace MyAspNetApp.Models.ViewModels
{
    public class LeaderboardPageViewModel
    {
        public string SeasonLabel { get; set; } = "Season 2026";
        public string Subtitle { get; set; } = "National Rankings";
        public List<LeaderboardEntryViewModel> Entries { get; set; } = new();
        public List<LeaderboardEntryViewModel> TopEntries => Entries.Take(3).ToList();
        public bool HasEntries => Entries.Count > 0;
    }

    public class LeaderboardEntryViewModel
    {
        public int Rank { get; set; }
        public int UploadId { get; set; }
        public int UserId { get; set; }
        public string AthleteName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string CoverImageUrl { get; set; } = string.Empty;
        public string DistanceDisplay { get; set; } = string.Empty;
        public string DurationDisplay { get; set; } = string.Empty;
        public string PaceDisplay { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string ChangeDisplay { get; set; } = "0";
        public string ChangeCssClass { get; set; } = "same";
        public decimal ProgressPercent { get; set; }
    }
}
