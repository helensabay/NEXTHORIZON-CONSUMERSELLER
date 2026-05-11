using MyAspNetApp.Models;
using MyAspNetApp.Models.Messaging;

namespace MyAspNetApp.Data.Messaging;

public interface IMessagingRepository
{
    Task<MessageConversationSummary> CreateOrGetGeneralAsync(int buyerUserId, int sellerUserId, CancellationToken cancellationToken);

    Task<MessageConversationSummary> CreateOrGetOrderAsync(int orderId, int buyerUserId, int sellerUserId, CancellationToken cancellationToken);

    Task<PagedResult<MessageConversationSummary>> ListByActorAsync(
        MessageActorContext actor,
        ConversationActorScope scope,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<MessageConversationSummary?> FindConversationAsync(
        MessageActorContext actor,
        ConversationContextType contextType,
        int? sellerUserId,
        int? orderId,
        CancellationToken cancellationToken);

    Task<MessageConversationSummary?> GetConversationAsync(int conversationId, MessageActorContext actor, CancellationToken cancellationToken);

    Task<MessageItem?> SendMessageAsync(
        int conversationId,
        MessageActorContext actor,
        string body,
        byte[]? attachmentData,
        string? attachmentContentType,
        string? attachmentFileName,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MessageItem>?> ListMessagesAsync(int conversationId, MessageActorContext actor, DateTime? before, int pageSize, CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(int conversationId, MessageActorContext actor, CancellationToken cancellationToken);

    Task<bool> SoftDeleteMessageAsync(long messageId, int userId, CancellationToken cancellationToken);
}

public sealed record MessageActorContext(
    int UserId,
    int? ConsumerId,
    int? SellerId);

public enum ConversationActorScope
{
    Any = 0,
    Consumer = 1,
    Seller = 2,
}

public sealed record MessageConversationSummary(
    int ConversationId,
    int BuyerUserId,
    int SellerUserId,
    ConversationContextType ContextType,
    int? OrderId,
    DateTime? LastMessageAt,
    DateTime? BuyerLastReadAt,
    DateTime? SellerLastReadAt,
    string? LastMessagePreview,
    int UnreadCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record MessageItem(
    long MessageId,
    int ConversationId,
    int SenderUserId,
    string? Body,
    string? AttachmentUrl,
    string? AttachmentContentType,
    string? AttachmentFileName,
    DateTime SentAt,
    bool IsDeleted);
