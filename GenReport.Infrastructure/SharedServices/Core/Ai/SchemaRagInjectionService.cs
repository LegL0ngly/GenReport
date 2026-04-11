using System.Text.Json;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.AI;
using Microsoft.Extensions.Logging;

namespace GenReport.Infrastructure.SharedServices.Core.Ai
{
    /// <inheritdoc />
    public sealed class SchemaRagInjectionService(
        ILogger<SchemaRagInjectionService> logger) : ISchemaRagInjectionService
    {
        /// <summary>
        /// The delimiter that separates the static system prompt from the mutable RAG JSON.
        /// </summary>
        internal const string Delimiter = "<<<GENREPORT_RAG_CONTEXT>>>";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <inheritdoc />
        public string BuildSystemMessage(string basePrompt, SchemaRagPayload payload)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            logger.LogDebug(
                "Building system message – provider={Provider}, db={Database}, objects={Count}",
                payload.DatabaseProvider, payload.DatabaseName, payload.Objects.Count);

            return $"{basePrompt}\n{Delimiter}\n{json}";
        }

        /// <inheritdoc />
        public SchemaRagPayload? ExtractPayload(string systemMessage)
        {
            var delimiterIndex = systemMessage.IndexOf(Delimiter, StringComparison.Ordinal);
            if (delimiterIndex < 0)
                return null;

            var jsonStart = delimiterIndex + Delimiter.Length;

            // Trim any leading newline between delimiter and JSON
            var jsonPortion = systemMessage[jsonStart..].TrimStart('\n', '\r');

            try
            {
                return JsonSerializer.Deserialize<SchemaRagPayload>(jsonPortion, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize RAG payload from system message");
                return null;
            }
        }

        /// <inheritdoc />
        public string ExtractBasePrompt(string systemMessage)
        {
            var delimiterIndex = systemMessage.IndexOf(Delimiter, StringComparison.Ordinal);
            if (delimiterIndex < 0)
                return systemMessage;

            // Strip the trailing newline before the delimiter
            return systemMessage[..delimiterIndex].TrimEnd('\n', '\r');
        }

        /// <inheritdoc />
        public string UpdatePayload(string existingSystemMessage, SchemaRagPayload updatedPayload)
        {
            var basePrompt = ExtractBasePrompt(existingSystemMessage);
            return BuildSystemMessage(basePrompt, updatedPayload);
        }

        /// <inheritdoc />
        public SchemaRagPayload BuildPayload(
            string databaseProvider,
            string databaseName,
            IReadOnlyList<SchemaSearchResult> searchResults)
        {
            var payload = new SchemaRagPayload
            {
                DatabaseProvider = databaseProvider,
                DatabaseName = databaseName,
                Objects = searchResults.Select(r => new SchemaRagObject
                {
                    Name = r.Name,
                    Type = r.Type.ToUpperInvariant(),
                    Schema = r.Schema,
                }).ToList()
            };

            logger.LogInformation(
                "Built RAG payload – provider={Provider}, db={Database}, objects={Count}",
                databaseProvider, databaseName, payload.Objects.Count);

            return payload;
        }
    }
}
