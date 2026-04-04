namespace GenReport.Infrastructure.Interfaces
{
    public record SchemaSearchResult(string Name, string Type, string FullSchema);

    /// <summary>
    /// Searches schema and routine objects for a given database using vector similarity.
    /// </summary>
    public interface ISchemaSearchService
    {
        /// <summary>
        /// Returns schema/routine objects semantically relevant to <paramref name="query"/>
        /// for the given <paramref name="databaseId"/>, filtered by cosine distance ≤ 0.4.
        /// </summary>
        Task<IReadOnlyList<SchemaSearchResult>> SearchAsync(
            string query,
            long databaseId,
            string provider,
            string apiKey,
            string model,
            CancellationToken ct = default);
    }
}
