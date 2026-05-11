using System.ComponentModel.DataAnnotations.Schema;

public class SupportMessage
{
    public int Id { get; set; }

    [Column("ConversationId")]
    public int SupportFAQId { get; set; }

    public int SenderId { get; set; }
    public string SenderRole { get; set; } // "Seller" or "Agent"
    public string MessageText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}