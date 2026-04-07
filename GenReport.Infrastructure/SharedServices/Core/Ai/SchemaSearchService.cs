using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace GenReport.Infrastructure.SharedServices.Core.Ai
{
    /// <summary>
    /// Performs vector similarity search over <c>schema_objects</c> and <c>routine_objects</c>
    /// for a given database, returning objects whose cosine distance to the query is ≤ 0.4.
    /// Dispatches to the appropriate <see cref="IEmbeddingService"/> based on the session's provider.
    /// </summary>
    public sealed class SchemaSearchService(
        ApplicationDbContext context,
        [FromKeyedServices("openai")] IEmbeddingService openAiEmbeddingService,
        [FromKeyedServices("ollama")] IEmbeddingService ollamaEmbeddingService,
        ILogger<SchemaSearchService> logger) : ISchemaSearchService
    {
        private const float CosineDistanceThreshold = 0.4f;

        public async Task<IReadOnlyList<SchemaSearchResult>> SearchAsync(
            string query,
            long databaseId,
            string provider,
            string apiKey,
            string model,
            CancellationToken ct = default)
        {
            // Resolve the right embedding service by provider name
            var normalised = provider.Trim().ToLowerInvariant();

            float[]? embedding = normalised switch
            {
                "openai" or "custom" => await TryOpenAiEmbeddingAsync(query, apiKey, model, ct),
                "ollama"             => await ollamaEmbeddingService.GenerateEmbeddingAsync(query, ct),
                _ => null
            };

            if (embedding == null)
            {
                logger.LogWarning(
                    "No embedding produced for provider '{Provider}'. Schema RAG will be skipped.", provider);
                return [];
            }

            var queryVector = new Vector(embedding);

            List<SchemaSearchResult> schemaResults;
            List<SchemaSearchResult> routineResults;

            if (normalised is "ollama")
            {
                // Ollama: compare against the 768-dim embedding_ollama column
                schemaResults = await context.SchemaObjects
                    .AsNoTracking()
                    .Where(s => s.DatabaseId == databaseId
                                && s.EmbeddingOllama != null
                                && s.FullSchema != null
                                && s.EmbeddingOllama.CosineDistance(queryVector) <= CosineDistanceThreshold)
                    .OrderBy(s => s.EmbeddingOllama!.CosineDistance(queryVector))
                    .Select(s => new SchemaSearchResult(s.Name, s.Type, s.FullSchema!))
                    .ToListAsync(ct);

                routineResults = await context.RoutineObjects
                    .AsNoTracking()
                    .Where(r => r.DatabaseId == databaseId
                                && r.EmbeddingOllama != null
                                && r.FullSchema != null
                                && r.EmbeddingOllama.CosineDistance(queryVector) <= CosineDistanceThreshold)
                    .OrderBy(r => r.EmbeddingOllama!.CosineDistance(queryVector))
                    .Select(r => new SchemaSearchResult(r.Name, r.Type, r.FullSchema!))
                    .ToListAsync(ct);
            }
            else
            {
                // OpenAI / Custom: compare against the 1536-dim embedding column
                schemaResults = await context.SchemaObjects
                    .AsNoTracking()
                    .Where(s => s.DatabaseId == databaseId
                                && s.Embedding != null
                                && s.FullSchema != null
                                && s.Embedding.CosineDistance(queryVector) <= CosineDistanceThreshold)
                    .OrderBy(s => s.Embedding!.CosineDistance(queryVector))
                    .Select(s => new SchemaSearchResult(s.Name, s.Type, s.FullSchema!))
                    .ToListAsync(ct);

                routineResults = await context.RoutineObjects
                    .AsNoTracking()
                    .Where(r => r.DatabaseId == databaseId
                                && r.Embedding != null
                                && r.FullSchema != null
                                && r.Embedding.CosineDistance(queryVector) <= CosineDistanceThreshold)
                    .OrderBy(r => r.Embedding!.CosineDistance(queryVector))
                    .Select(r => new SchemaSearchResult(r.Name, r.Type, r.FullSchema!))
                    .ToListAsync(ct);
            }

            var combined = schemaResults.Concat(routineResults).ToList();

            logger.LogInformation(
                "Schema RAG: found {Count} relevant object(s) for database {DbId} (schema: {S}, routines: {R})",
                combined.Count, databaseId, schemaResults.Count, routineResults.Count);

            return combined;
        }

        /// <summary>
        /// Delegates to <see cref="OpenAIEmbeddingService"/> passing the session-scoped key/model.
        /// </summary>
        private async Task<float[]?> TryOpenAiEmbeddingAsync(
            string query, string apiKey, string model, CancellationToken ct)
        {
            if (openAiEmbeddingService is OpenAIEmbeddingService typedService)
                return await typedService.GenerateEmbeddingAsync(query, apiKey, model, ct);

            return await openAiEmbeddingService.GenerateEmbeddingAsync(query, ct);
        }
    }
}
