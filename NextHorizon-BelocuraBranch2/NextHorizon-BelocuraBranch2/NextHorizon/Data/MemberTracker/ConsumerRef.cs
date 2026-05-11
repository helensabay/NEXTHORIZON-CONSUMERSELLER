namespace NextHorizon.Data;

public sealed class ConsumerRef
{
    public int ConsumerId { get; set; }

    public int UserId { get; set; }

    public string? FirstName { get; set; }

    public string? MiddleName { get; set; }

    public string? LastName { get; set; }

    public string? Username { get; set; }
}