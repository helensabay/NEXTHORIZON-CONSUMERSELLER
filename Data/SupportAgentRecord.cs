namespace MyAspNetApp.Data;

public sealed class SupportAgentRecord
{
    public int ChatId { get; set; }

    public string? AgentName { get; set; }

    public string? AgentStatus { get; set; }

    public int UserId { get; set; }
}
