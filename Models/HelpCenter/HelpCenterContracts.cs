using System.ComponentModel.DataAnnotations;

namespace MyAspNetApp.Models.HelpCenter;

public sealed record HelpCategorySummaryDto(
    string Slug,
    string Title,
    string Description,
    string IconKey,
    int DisplayOrder);

public sealed record HelpFaqDto(
    int Id,
    string Question,
    string Answer,
    int DisplayOrder);

public sealed record HelpCategoryDetailDto(
    string Slug,
    string Title,
    string Description,
    string IconKey,
    int DisplayOrder,
    IReadOnlyList<HelpFaqDto> Faqs);

public sealed record HelpFeaturedFaqDto(
    int Id,
    string Question,
    string Answer,
    string CategorySlug,
    string CategoryTitle);

public sealed record HelpHomeResponseDto(
    IReadOnlyList<HelpCategorySummaryDto> Categories,
    IReadOnlyList<HelpFeaturedFaqDto> FeaturedFaqs);

public sealed record HelpSearchResultDto(
    int Id,
    string Question,
    string Answer,
    string CategorySlug,
    string CategoryTitle);

public sealed record SupportContactDto(
    string ChannelType,
    string Label,
    string Value,
    string DisplayText,
    string ActionHref,
    int DisplayOrder);

public sealed record HelpContactResponseDto(
    IReadOnlyList<SupportContactDto> Channels);

public sealed class CreateSupportTicketRequest
{
    [Required]
    [StringLength(160, MinimumLength = 5)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 20)]
    public string Body { get; set; } = string.Empty;

    [StringLength(80)]
    public string? CategorySlug { get; set; }
}

public sealed record CreateSupportTicketResponse(
    int TicketId,
    string ReferenceCode,
    string Status,
    DateTime CreatedAt);

public sealed class CreateLiveAgentSessionRequest
{
    [Required]
    [StringLength(80)]
    public string CategorySlug { get; set; } = string.Empty;
}

public sealed class SelectAssistantCategoryRequest
{
    [Required]
    [StringLength(80)]
    public string CategorySlug { get; set; } = string.Empty;
}

public sealed record LiveAgentSessionResponse(
    int SessionId,
    int SupportFaqId,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CategorySlug,
    string CategoryTitle,
    bool FirstQuestionCaptured,
    bool HasAssignedAgent,
    string? AssignedAgentName,
    IReadOnlyList<LiveAgentMessageDto> Messages);

public sealed record LiveAgentMessageDto(
    int MessageId,
    int ConversationId,
    int SenderId,
    string SenderRole,
    string MessageText,
    DateTime CreatedAt);

public sealed class CaptureLiveAgentQuestionRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

public sealed record CaptureLiveAgentQuestionResponse(
    int SessionId,
    int SupportFaqId,
    string Status,
    string Question,
    DateTime UpdatedAt);

public sealed class AppendLiveAgentMessageRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

public sealed record AppendLiveAgentMessageResponse(
    int SessionId,
    int SupportFaqId,
    string Status,
    bool FirstQuestionCaptured,
    bool HasAssignedAgent,
    string? AssignedAgentName,
    DateTime UpdatedAt,
    IReadOnlyList<LiveAgentMessageDto> Messages);

public sealed record ResolveLiveAgentSessionResponse(
    int SessionId,
    string Status,
    DateTime UpdatedAt);

public sealed record LastEndedLiveAgentNoticeResponse(
    int SessionId,
    string CategoryTitle,
    string EndedReason,
    DateTime EndedAt);
