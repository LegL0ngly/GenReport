using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using GenReport.Domain.Entities.Onboarding;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace GenReport.DB.Domain.Entities.Core
{
    [Table("chat_sessions")]
    public class ChatSession : BaseEntity
    {
        [Column("user_id")]
        public long UserId { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        [System.Text.Json.Serialization.JsonIgnore]
        public User User { get; set; }
#pragma warning restore CS8618

        [Column("title")]
        public string? Title { get; set; }

        [Column("model_id")]
        public string? ModelId { get; set; }

        /// <summary>The AI provider connection used for this session. Null until a connection is assigned.</summary>
        [Column("ai_connection_id")]
        public long? AiConnectionId { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public AiConnection? AiConnection { get; set; }

        /// <summary>The database selected by the user for this session. Used for schema RAG.</summary>
        [Column("database_id")]
        public long? DatabaseId { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public Database? Database { get; set; }

        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
