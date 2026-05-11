namespace NextHorizon.Messaging.Models;

public class ConversationMessage
{
    public long MessageId { get; set; }

    public int ConversationId { get; set; }

    public int SenderUserId { get; set; }

    public string Body { get; set; } = string.Empty;

    public string? AttachmentUrl { get; set; }

    public byte[]? AttachmentData { get; set; }

    public string? AttachmentContentType { get; set; }

    public string? AttachmentFileName { get; set; }

    public DateTime SentAt { get; set; }

    public bool IsDeleted { get; set; }

    public MessageConversation Conversation { get; set; } = null!;
}
