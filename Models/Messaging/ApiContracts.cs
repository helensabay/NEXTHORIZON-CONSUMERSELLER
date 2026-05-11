namespace MyAspNetApp.Models.Messaging;

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
    public string? AttachmentContentType { get; init; }
    public string? AttachmentFileName { get; init; }
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

public sealed class StorefrontFaqDto
{
    public int FaqId { get; init; }
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
}

public sealed class StorefrontFaqClickRequest
{
    public int FaqId { get; init; }
    public string SellerUserId { get; init; } = string.Empty;
}

public sealed class StorefrontOrderedProductDto
{
    public int ProductId { get; init; }
    public int OrderId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public decimal? Price { get; init; }
    public int Stock { get; init; }
    public string ProductUrl { get; init; } = string.Empty;
    public string StoreUrl { get; init; } = string.Empty;
    public string SellerUserId { get; init; } = string.Empty;
}
