namespace NextHorizon.Messaging.Models;

public class MessageConversation
{
    public int ConversationId { get; set; }

    public int BuyerUserId { get; set; }

    public int SellerUserId { get; set; }

    public ConversationContextType ContextType { get; set; }

    public int? OrderId { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public DateTime? BuyerLastReadAt { get; set; }

    public DateTime? SellerLastReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}