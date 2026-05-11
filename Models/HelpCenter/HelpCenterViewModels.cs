namespace MyAspNetApp.Models.HelpCenter
{
    public sealed record HelpCenterCategoryCard(
        string Slug,
        string Title,
        string Description,
        string IconClass,
        int DisplayOrder);

    public sealed record HelpCenterFaqItem(
        int Id,
        string Question,
        string Answer,
        int DisplayOrder);

    public sealed record HelpCenterHomeViewModel(
        IReadOnlyList<HelpCenterCategoryCard> Categories,
        IReadOnlyList<HelpCenterFaqItem> FeaturedFaqs);

    public sealed record HelpCenterTopicViewModel(
        string Slug,
        string Title,
        string Description,
        string IconClass,
        IReadOnlyList<HelpCenterFaqItem> Faqs);

    public sealed record HelpCenterContactChannel(
        string ChannelType,
        string Label,
        string Value,
        string ActionHref);

    public sealed record HelpCenterContactViewModel(
        string Title,
        string Description,
        IReadOnlyList<HelpCenterContactChannel> Channels);

    public sealed record HelpCenterAssistantViewModel(
        IReadOnlyList<HelpCenterCategoryCard> Categories,
        IReadOnlyList<HelpCenterFaqItem> QuickFaqs);
}
