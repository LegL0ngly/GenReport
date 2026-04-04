using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using Pgvector;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.DB.Domain.Entities.Core
{
    /// <summary>
    /// Represents a routine object (stored procedure or function) from a database with its vector embedding.
    /// </summary>
    [Table("routine_objects")]
    public class RoutineObject : BaseEntity
    {
        /// <summary>
        /// Gets or sets the database ID to which this routine object belongs.
        /// </summary>
        [Required]
        [Column("database_id")]
        public long DatabaseId { get; set; }

        /// <summary>
        /// Navigation property to the database.
        /// </summary>
        [ForeignKey("database_id")]
        public virtual Database Database { get; set; } = null!;

        /// <summary>
        /// Gets or sets the name of the routine object.
        /// </summary>
        [Required]
        [Column("name")]
        [StringLength(255)]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the type ('sp' or 'function').
        /// </summary>
        [Required]
        [Column("type")]
        [StringLength(10)]
        public required string Type { get; set; }

        /// <summary>
        /// Gets or sets the intent description used for generating the embedding.
        /// </summary>
        [Column("embedding_text")]
        public string? EmbeddingText { get; set; }

        /// <summary>
        /// Gets or sets the full body or signature of the routine.
        /// </summary>
        [Column("full_schema")]
        public string? FullSchema { get; set; }

        /// <summary>
        /// Gets or sets the vector embedding of the routine object.
        /// </summary>
        [Column("embedding", TypeName = "vector(1536)")]
        public Vector? Embedding { get; set; }

        /// <summary>
        /// Gets or sets additional metadata in JSONB format.
        /// </summary>
        [Column("metadata", TypeName = "jsonb")]
        public string? Metadata { get; set; }
    }
}
