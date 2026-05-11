namespace NextHorizon.Messaging.Models;

public sealed class ConversationDto
{
    public int ConversationId { get; init; }
    public string CurrentUserId { get; init; } = string.Empty;
    public string BuyerUserId { get; init; } = string.Empty;
    public string SellerUserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DisplaySubtitle { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public string ContextLabel { get; init; } = string.Empty;
    public string CounterpartyRole { get; init; } = string.Empty;
    public string CounterpartyId { get; init; } = string.Empty;
    public bool CanReply { get; init; }
    public string ContextType { get; init; } = string.Empty;
    public int? OrderId { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public DateTime? BuyerLastReadAt { get; init; }
    public DateTime? SellerLastReadAt { get; init; }
    public string? LastMessagePreview { get; init; }
    public int UnreadCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class MessageDto
{
    public long MessageId { get; init; }
    public int ConversationId { get; init; }
    public string SenderUserId { get; init; } = string.Empty;
    public string? Body { get; init; }
    public string? AttachmentUrl { get; init; }
    public bool HasAttachment { get; init; }
    public DateTime SentAt { get; init; }
    public bool IsDeleted { get; init; }
}

public sealed class CreateConversationRequest
{
    public string ContextType { get; init; } = string.Empty;
    public string? SellerUserId { get; init; }
    public int? OrderId { get; init; }
}

public sealed class ConversationListQuery
{
    public string? Role { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed class ResolveConversationQuery
{
    public string ContextType { get; init; } = string.Empty;
    public string? SellerUserId { get; init; }
    public int? OrderId { get; init; }
}

public sealed class MessageListQuery
{
    public string? Role { get; init; }
    public DateTime? Before { get; init; }
    public int? PageSize { get; init; }
}

public sealed class SendMessageRequest
{
    public string? Body { get; init; }
    public IFormFile? Attachment { get; init; }
}
