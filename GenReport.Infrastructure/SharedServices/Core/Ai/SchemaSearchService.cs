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
    /// Performs schema and routine object retrieval using <b>Multi-Query Expansion</b> (MQE)
    /// combined with <b>Reciprocal Rank Fusion</b> (RRF) over pgvector cosine-distance searches.
    /// <para>
    /// Pipeline:
    /// <list type="number">
    ///   <item>The original query is expanded into N variants via <see cref="IQueryExpansionService"/>.</item>
    ///   <item>Each variant is embedded in parallel (embedding services are thread-safe).</item>
    ///   <item>Each embedded variant runs its own vector search in parallel against
    ///         <c>schema_objects</c> and <c>routine_objects</c>, each using a short-lived
    ///         <see cref="ApplicationDbContext"/> created from <see cref="IDbContextFactory{TContext}"/>
    ///         so that concurrent DB access is safe.</item>
    ///   <item>RRF merges all ranked result lists into one deduplicated ranking
    ///         (score = Σ 1 / (60 + rank) across query lists).</item>
    ///   <item>Top <see cref="MaxResults"/> objects by RRF score are returned.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class SchemaSearchService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IQueryExpansionService queryExpansionService,
        [FromKeyedServices("openai")] IEmbeddingService openAiEmbeddingService,
        [FromKeyedServices("ollama")] IEmbeddingService ollamaEmbeddingService,
        ILogger<SchemaSearchService> logger) : ISchemaSearchService
    {
        // Slightly relaxed vs. the old 0.4 — RRF naturally demotes weak single-query matches.
        private const float CosineDistanceThreshold = 0.45f;

        // Standard RRF constant (k=60 recommended in the original Cormack et al. paper).
        private const int RrfK = 60;

        // Cap the final merged result to keep the injected schema token budget manageable.
        private const int MaxResults = 10;

        /// <inheritdoc />
        public async Task<IReadOnlyList<SchemaSearchResult>> SearchAsync(
            string query,
            long databaseId,
            string provider,
            string apiKey,
            string model,
            CancellationToken ct = default)
        {
            var normalised = provider.Trim().ToLowerInvariant();

            // ── Step 1: Expand the query into multiple variants ───────────────────────
            var queries = await queryExpansionService.ExpandAsync(query, provider, apiKey, model, ct);

            // ── Step 2: Embed all variants in parallel ────────────────────────────────
            // Safe to parallelise — embedding services do not use DbContext.
            var embeddingTasks = queries
                .Select(q => EmbedQueryAsync(q, normalised, apiKey, model, ct))
                .ToList();

            var embeddings = await Task.WhenAll(embeddingTasks);

            // Build (query, Vector) pairs — skip any that failed to embed
            var validPairs = queries
                .Zip(embeddings, (q, e) => (Query: q, Embedding: e))
                .Where(pair => pair.Embedding != null)
                .Select(pair => (pair.Query, Vector: new Vector(pair.Embedding!)))
                .ToList();

            if (validPairs.Count == 0)
            {
                logger.LogWarning(
                    "No embeddings produced for any expanded query (provider='{Provider}'). Schema RAG skipped.",
                    provider);
                return [];
            }

            // ── Step 3: Search in parallel — each task owns its own DbContext ─────────
            // IDbContextFactory creates an independent DbContext per call, so concurrent
            // EF operations are isolated and thread-safe.
            var searchTasks = validPairs
                .Select(pair => SearchForVectorAsync(pair.Vector, databaseId, normalised, ct))
                .ToList();

            var perQueryResults = await Task.WhenAll(searchTasks);

            // ── Step 4: Reciprocal Rank Fusion ────────────────────────────────────────
            // key = (Name, Type) as a unique identity for deduplication across lists
            var rrfScores = new Dictionary<(string Name, string Type), double>();
            var resultsByKey = new Dictionary<(string Name, string Type), SchemaSearchResult>();

            foreach (var resultList in perQueryResults)
            {
                for (int rank = 0; rank < resultList.Count; rank++)
                {
                    var item = resultList[rank];
                    var key = (item.Name, item.Type);

                    var contribution = 1.0 / (RrfK + rank + 1); // 1-indexed rank
                    rrfScores.TryAdd(key, 0.0);
                    rrfScores[key] += contribution;

                    resultsByKey.TryAdd(key, item); // first occurrence wins for FullSchema
                }
            }

            var merged = rrfScores
                .OrderByDescending(kv => kv.Value)
                .Take(MaxResults)
                .Select(kv => resultsByKey[kv.Key])
                .ToList();

            logger.LogInformation(
                "Schema RAG (RRF): {Total} unique object(s) from {QueryCount} query variant(s) " +
                "(database={DbId}, schema+routines per query: {PerQuery}, returned after cap: {Returned}).",
                rrfScores.Count,
                validPairs.Count,
                databaseId,
                string.Join(", ", perQueryResults.Select(r => r.Count)),
                merged.Count);

            return merged;
        }

        // ── Private helpers ───────────────────────────────────────────────────────────

        /// <summary>   
        /// Generates an embedding for <paramref name="query"/> using the appropriate service.
        /// </summary>
        private async Task<float[]?> EmbedQueryAsync(
            string query, string normalised, string apiKey, string model, CancellationToken ct)
        {
            return normalised switch
            {
                "openai" or "custom" => await TryOpenAiEmbeddingAsync(query, apiKey, model, ct),
                "ollama"             => await ollamaEmbeddingService.GenerateEmbeddingAsync(query, ct),
                _ => null
            };
        }

        /// <summary>
        /// Runs a cosine-distance search for a single query vector against both
        /// <c>schema_objects</c> and <c>routine_objects</c> using a fresh, independently
        /// owned <see cref="ApplicationDbContext"/> from the factory — safe for concurrent use.
        /// </summary>
        private async Task<List<SchemaSearchResult>> SearchForVectorAsync(
            Vector queryVector,
            long databaseId,
            string normalised,
            CancellationToken ct)
        {
            // Each parallel task gets its own context — fully isolated, no shared state.
            await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

            List<SchemaSearchResult> schemaResults;
            List<SchemaSearchResult> routineResults;

            if (normalised is "ollama")
            {
                schemaResults = await ctx.SchemaObjects
                    .AsNoTracking()
                    .Where(s => s.DatabaseId == databaseId
                                && s.EmbeddingOllama != null
                                && s.EmbeddingText != null
                                && s.EmbeddingOllama.CosineDistance(queryVector) <= CosineDistanceThreshold)
                    .OrderBy(s => s.EmbeddingOllama!.CosineDistance(queryVector))
                    .Select(s => new SchemaSearchResult(s.Name, s.Type, s.EmbeddingText!))
                    .ToListAsync(ct);

                routineResults = await ctx.RoutineObjects
                    .AsNoTracking()
                    .Where(r => r.DatabaseId == databaseId
                                && r.EmbeddingOllama != null
                                && r.EmbeddingText != null
                                && r.EmbeddingOllama.CosineDistance(queryVector) <= CosineDistanceThreshold)
                    .OrderBy(r => r.EmbeddingOllama!.CosineDistance(queryVector))
                    .Select(r => new SchemaSearchResult(r.Name, r.Type, r.EmbeddingText!))
                    .ToListAsync(ct);
            }
            else
            {
                // OpenAI / Custom: 1536-dim embedding column
                schemaResults = await ctx.SchemaObjects
                    .AsNoTracking()
                    .Where(s => s.DatabaseId == databaseId
                                && s.Embedding != null
                                && s.EmbeddingText != null
                                && s.Embedding.CosineDistance(queryVector) <= CosineDistanceThreshold)
                    .OrderBy(s => s.Embedding!.CosineDistance(queryVector))
                    .Select(s => new SchemaSearchResult(s.Name, s.Type, s.EmbeddingText!))
                    .ToListAsync(ct);

                routineResults = await ctx.RoutineObjects
                    .AsNoTracking()
                    .Where(r => r.DatabaseId == databaseId
                                && r.Embedding != null
                                && r.EmbeddingText != null
                                && r.Embedding.CosineDistance(queryVector) <= CosineDistanceThreshold)
                    .OrderBy(r => r.Embedding!.CosineDistance(queryVector))
                    .Select(r => new SchemaSearchResult(r.Name, r.Type, r.EmbeddingText!))
                    .ToListAsync(ct);
            }

            return [.. schemaResults, .. routineResults];
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
