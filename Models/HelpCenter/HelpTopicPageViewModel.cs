namespace MyAspNetApp.Models.HelpCenter;

public sealed record HelpTopicPageViewModel(string CategorySlug, bool IsContactPage = false);
