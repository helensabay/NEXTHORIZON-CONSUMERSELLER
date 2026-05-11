using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("SupportConversations")]
public class SupportConversation
{
    [Key]
    public int Id { get; set; }

    public int SellerId { get; set; }

    public int? AgentId { get; set; }

    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}