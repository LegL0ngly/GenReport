using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.DB.Domain.Entities.Core
{
    [Table("chat_messages")]
    public class ChatMessage : BaseEntity
    {
        /// <summary>Classified intent of the message (set only for user messages). Null for assistant messages.</summary>
        [Column("intent")]
        [StringLength(50)]
        public string? Intent { get; set; }

        [Column("session_id")]
        public long SessionId { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
        [System.Text.Json.Serialization.JsonIgnore]
        public ChatSession Session { get; set; }
#pragma warning restore CS8618

        [Column("role")]
        public required string Role { get; set; } // 'user' | 'assistant'

        [Column("content")]
        public required string Content { get; set; }

        public ICollection<MessageReport> Reports { get; set; } = new List<MessageReport>();
        public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
    }
}
