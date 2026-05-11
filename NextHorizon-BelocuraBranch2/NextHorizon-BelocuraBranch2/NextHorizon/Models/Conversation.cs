using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NextHorizon.Models;

public class Conversation
{
    public int Id { get; set; }

    [Required]
    public string ConsumerId { get; set; } = string.Empty;

    [Required]
    public string SellerId { get; set; } = string.Empty;

    [Required]
    public string ContextType { get; set; } = string.Empty;

    public int? OrderId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Initialize Messages collection to empty to avoid null warnings
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}