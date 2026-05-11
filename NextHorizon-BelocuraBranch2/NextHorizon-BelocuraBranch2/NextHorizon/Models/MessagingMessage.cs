using System;
using System.ComponentModel.DataAnnotations;

namespace NextHorizon.Models
{
    public class MessagingMessage
    {
        [Key]
        public int Id { get; set; } // Primary key

        [Required]
        public int ConversationId { get; set; } // ID of the conversation

        [Required]
        [MaxLength(50)]
        public string SenderType { get; set; } // e.g., "Seller" or "Consumer"

        [Required]
        [MaxLength(1000)]
        public string MessageText { get; set; } // The message content

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Timestamp
    }
}