using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace GenReport.Infrastructure.SharedServices.Core.Ai
{
    /// <summary>
    /// Performs vector similarity search over <c>schema_objects</c> and <c>routine_objects</c>
    /// for a given database, returning objects whose cosine distance to the query is ≤ 0.4.
    /// </summary>
    public sealed class SchemaSearchService(
        ApplicationDbContext context,
        IEmbeddingService embeddingService,
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
            var embedding = await embeddingService.GenerateEmbeddingAsync(query, provider, apiKey, model, ct);

            if (embedding == null)
                return [];

            var queryVector = new Vector(embedding);

            // Search schema objects (tables, views)
            var schemaResults = await context.SchemaObjects
                .AsNoTracking()
                .Where(s => s.DatabaseId == databaseId
                            && s.Embedding != null
                            && s.FullSchema != null
                            && s.Embedding.CosineDistance(queryVector) <= CosineDistanceThreshold)
                .OrderBy(s => s.Embedding!.CosineDistance(queryVector))
                .Select(s => new SchemaSearchResult(s.Name, s.Type, s.FullSchema!))
                .ToListAsync(ct);

            // Search routine objects (stored procedures, functions)
            var routineResults = await context.RoutineObjects
                .AsNoTracking()
                .Where(r => r.DatabaseId == databaseId
                            && r.Embedding != null
                            && r.FullSchema != null
                            && r.Embedding.CosineDistance(queryVector) <= CosineDistanceThreshold)
                .OrderBy(r => r.Embedding!.CosineDistance(queryVector))
                .Select(r => new SchemaSearchResult(r.Name, r.Type, r.FullSchema!))
                .ToListAsync(ct);

            var combined = schemaResults.Concat(routineResults).ToList();

            logger.LogInformation(
                "Schema RAG: found {Count} relevant object(s) for database {DbId} (schema: {S}, routines: {R})",
                combined.Count, databaseId, schemaResults.Count, routineResults.Count);

            return combined;
        }
    }
}
