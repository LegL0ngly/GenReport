namespace GenReport.Infrastructure.Interfaces
{
    /// <summary>
    /// Generates vector embeddings for text using the configured AI provider.
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Generates a floating-point vector embedding for the given text.
        /// </summary>
        Task<float[]?> GenerateEmbeddingAsync(string text, string provider, string apiKey, string model, CancellationToken ct = default);
    }
}
