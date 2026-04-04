using CoreDdd.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using GenReport.DB.Domain.Common;

namespace GenReport.DB.Domain.Entities.Core
{
    /// <summary>
    /// Versioned AI configuration. Each row represents one version of a config
    /// identified by <see cref="Type"/> (e.g. 1 for IntentClassifier, 2 for ChatSystemPrompt),
    /// the associated <see cref="AiConnectionId"/>, and optionally <see cref="ModelId"/>.
    /// Only one row per Type + AiConnectionId + ModelId should have <see cref="IsActive"/> = true.
    /// </summary>
    [Table("ai_configs")]
    public class AiConfig : BaseEntity
    {
        /// <summary>The type of this configuration (e.g. IntentClassifier, ChatSystemPrompt).</summary>
        [Column("type")]
        [Required]
        public required AiConfigType Type { get; set; }

        /// <summary>The content of this configuration (e.g. system prompt or instructions text).</summary>
        [Column("value")]
        [Required]
        public required string Value { get; set; }

        /// <summary>The AI connection this configuration applies to.</summary>
        [Column("ai_connection_id")]
        public long AiConnectionId { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public AiConnection AiConnection { get; set; } = null!;

        /// <summary>Optional model override (e.g. "gemini-2.0-flash-lite"). Specifies which model this config targets for the connection.</summary>
        [Column("model_id")]
        [StringLength(100)]
        public string? ModelId { get; set; }

        /// <summary>Whether this is the currently active config for its Type + AiConnectionId + ModelId.</summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Auto-incrementing version number within the same Type + AiConnectionId + ModelId.</summary>
        [Column("version")]
        public int Version { get; set; } = 1;

    }
}
