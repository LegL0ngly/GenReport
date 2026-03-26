using CoreDdd.Domain;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.DB.Domain.Entities.Core
{
    [Table("chat_messages")]
    public class ChatMessage : Entity<long>, IAggregateRoot
    {
        [Column("session_id")]
        public long SessionId { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
        public ChatSession Session { get; set; }
#pragma warning restore CS8618

        [Column("role")]
        public required string Role { get; set; } // 'user' | 'assistant'

        [Column("content")]
        public required string Content { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<MessageReport> Reports { get; set; } = new List<MessageReport>();
        public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
    }
}
