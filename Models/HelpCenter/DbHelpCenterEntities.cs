using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyAspNetApp.Models.HelpCenter;

public enum LiveAgentSessionStatus : byte
{
    Waiting = 1,
    Active = 2,
    Resolved = 3,
}

public enum LiveAgentSessionEndedReason : byte
{
    None = 0,
    Resolved = 1,
    Inactive = 2,
}

public enum SupportTicketStatus : byte
{
    Open = 1,
    InProgress = 2,
    Resolved = 3,
}

[Table("FAQs")]
public sealed class FaqRecord
{
    [Key]
    [Column("FaqID")]
    public int FaqId { get; set; }

    [MaxLength(500)]
    public string Question { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Answer { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Status { get; set; } = string.Empty;

    [Column("user_id")]
    public int UserId { get; set; }

    public DateTime DateAdded { get; set; }

    public DateTime LastUpdated { get; set; }

    [MaxLength(40)]
    public string UserType { get; set; } = string.Empty;
}

[Table("SupportFAQs")]
public sealed class SupportFaqRecord
{
    [Key]
    public int Id { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public string UserType { get; set; } = string.Empty;

    public int? AgentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EndTime { get; set; }

    public DateTime? StartTime { get; set; }
}

[Table("SupportContactChannels")]
public sealed class SupportContactChannel
{
    [Key]
    public int SupportContactChannelId { get; set; }

    [MaxLength(40)]
    public string ChannelType { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(320)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(320)]
    public string DisplayText { get; set; } = string.Empty;

    [MaxLength(400)]
    public string ActionHref { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

[Table("SupportTickets")]
public sealed class SupportTicket
{
    [Key]
    public int SupportTicketId { get; set; }

    [MaxLength(40)]
    public string ReferenceCode { get; set; } = string.Empty;

    public int UserId { get; set; }

    public int? ConsumerId { get; set; }

    [MaxLength(120)]
    public string? FaqCategory { get; set; }

    [MaxLength(160)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

[Table("SupportMessages")]
public sealed class SupportMessage
{
    [Key]
    public int Id { get; set; }

    public int ConversationId { get; set; }

    public int SenderId { get; set; }

    public string SenderRole { get; set; } = string.Empty;

    public string MessageText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

[Table("LiveAgentSessions")]
public sealed class LiveAgentSession
{
    [Key]
    public int LiveAgentSessionId { get; set; }

    public int SupportFaqId { get; set; }

    public int UserId { get; set; }

    public int? ConsumerId { get; set; }

    [MaxLength(80)]
    public string CategorySlug { get; set; } = string.Empty;

    [MaxLength(120)]
    public string CategoryTitle { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string FirstQuestion { get; set; } = string.Empty;

    public LiveAgentSessionStatus Status { get; set; } = LiveAgentSessionStatus.Waiting;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public LiveAgentSessionEndedReason EndedReason { get; set; } = LiveAgentSessionEndedReason.None;
}
