using System.Text.Json.Serialization;

namespace GenReport.Infrastructure.Models.AI
{
    /// <summary>
    /// Strongly-typed model for the RAG JSON payload that is injected
    /// into the system message alongside the static system prompt.
    /// </summary>
    public class SchemaRagPayload
    {
        /// <summary>The database engine, e.g. "PostgreSQL", "SqlServer", "MySQL".</summary>
        [JsonPropertyName("databaseProvider")]
        public string DatabaseProvider { get; set; } = string.Empty;

        /// <summary>The user-facing database name.</summary>
        [JsonPropertyName("databaseName")]
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>The relevant schema/routine objects for the current query context.</summary>
        [JsonPropertyName("objects")]
        public List<SchemaRagObject> Objects { get; set; } = [];
    }

    /// <summary>
    /// A single schema or routine object inside the RAG payload.
    /// </summary>
    public class SchemaRagObject
    {
        /// <summary>Object name, e.g. "orders", "fn_get_total".</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Object type, e.g. "TABLE", "VIEW", "FUNCTION", "PROCEDURE".</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>Concise embedding text describing the object's structure (table/column names, types, FK relationships).</summary>
        [JsonPropertyName("schema")]
        public string Schema { get; set; } = string.Empty;
    }
}
