using CoreDdd.Domain;
using GenReport.DB.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.DB.Domain.Entities.Core
{
    /// <summary>
    /// Represents an API endpoint belonging to an <see cref="AiConnection"/>.
    /// Three rows (Chat, Models, Quota) are seeded automatically when a connection is created.
    /// </summary>
    [Table("ai_model_endpoints")]
    public class AiModelEndpoint : Entity<long>, IAggregateRoot
    {
        /// <summary>Foreign key to the parent <see cref="AiConnection"/>.</summary>
        [Column("ai_connection_id")]
        [Required]
        public long AiConnectionId { get; set; }

        /// <summary>
        /// Endpoint category.
        /// </summary>
        [Column("endpoint_type")]
        [Required]
        public AiEndpointType EndpointType { get; set; }

        /// <summary>
        /// Relative path for this endpoint (e.g. /v1/chat/completions).
        /// </summary>
        [Column("path")]
        [Required]
        [StringLength(500)]
        public required string Path { get; set; }

        /// <summary>HTTP method — GET or POST.</summary>
        [Column("http_method")]
        [Required]
        [StringLength(10)]
        public required string HttpMethod { get; set; }

        /// <summary>Whether this endpoint is enabled and callable.</summary>
        [Column("is_enabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>Optional free-text notes about this endpoint.</summary>
        [Column("notes")]
        [StringLength(500)]
        public string? Notes { get; set; }

        /// <summary>Navigation property back to the parent connection.</summary>
        public AiConnection? AiConnection { get; set; }
    }
}
