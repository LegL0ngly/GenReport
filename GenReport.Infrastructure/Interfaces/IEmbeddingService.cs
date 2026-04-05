namespace GenReport.Infrastructure.Interfaces
{
    /// <summary>
    /// Generates vector embeddings for text using a specific AI provider.
    /// Each implementation is self-contained and resolves its own configuration.
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Generates a floating-point vector embedding for the given text.
        /// Returns null if the provider is unavailable or the operation fails.
        /// </summary>
        Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    }
}
