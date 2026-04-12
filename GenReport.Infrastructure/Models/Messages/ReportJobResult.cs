using System.Text.Json.Serialization;

namespace GenReport.Infrastructure.Models.Messages
{
    /// <summary>
    /// Mirrors the Go <c>ReportJobResult</c> struct published by the Go report worker
    /// to the <c>report_success</c> and <c>report_error</c> RabbitMQ queues.
    /// </summary>
    public sealed class ReportJobResult
    {
        /// <summary>Database connection ID from the original request.</summary>
        [JsonPropertyName("databaseConnectionId")]
        public string DatabaseConnectionId { get; set; } = string.Empty;

        /// <summary>Requested format (e.g. "excel", "pdf").</summary>
        [JsonPropertyName("format")]
        public string Format { get; set; } = string.Empty;

        /// <summary>The raw SQL query that was executed.</summary>
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        /// <summary>Chat session ID that triggered this report.</summary>
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Absolute path to the generated SQLite file on the local filesystem.
        /// Populated on success; empty on failure.
        /// </summary>
        [JsonPropertyName("sqliteFilePath")]
        public string? SqliteFilePath { get; set; }

        /// <summary>
        /// Human-readable error message. Populated on failure; empty on success.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        /// <summary>Returns true when this message represents a successful job.</summary>
        [JsonIgnore]
        public bool IsSuccess => string.IsNullOrWhiteSpace(Error);
    }
}
