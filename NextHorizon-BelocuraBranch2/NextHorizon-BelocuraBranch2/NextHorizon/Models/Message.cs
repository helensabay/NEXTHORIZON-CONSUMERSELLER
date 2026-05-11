using System;
using System.ComponentModel.DataAnnotations;

namespace NextHorizon.Models;

public class Message
{
    public int Id { get; set; }   // Primary Key

    [Required]
    public int ConversationId { get; set; }

    public Conversation Conversation { get; set; } = null!; // required, non-nullable

    [Required]
    public string SenderId { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public string AttachmentUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;

}