using CoreDdd.Domain;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace GenReport.DB.Domain.Common
{
    public abstract class BaseEntity : Entity<long>, IAggregateRoot
    {
        /// <summary>
        /// Overridden to serialize as a JSON string so the frontend always receives a
        /// consistent string type (e.g. "42" not 42). The DB still stores a long.
        /// </summary>
        [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
        public new long Id => base.Id;

        [Column("created_at")]
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
