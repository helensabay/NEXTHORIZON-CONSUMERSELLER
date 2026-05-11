using System.ComponentModel.DataAnnotations.Schema;

public class SupportFAQ
{
    public int Id { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public string Status { get; set; } = "Waiting";

    public int DurationMinutes { get; set; }

    public string UserType { get; set; } = string.Empty;

    public int? AgentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }
}