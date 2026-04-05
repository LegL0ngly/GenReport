namespace GenReport.Infrastructure.Configuration
{
    /// <summary>
    /// Strongly-typed options for the locally-hosted Ollama service.
    /// Bind from the "Ollama" config section.
    /// </summary>
    public sealed class OllamaOptions
    {
        public const string SectionName = "Ollama";

        /// <summary>
        /// Base URL of the Ollama HTTP API, e.g. http://localhost:11434
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:11434";

        /// <summary>
        /// Default embedding model — nomic-embed-text produces 768-dimension vectors.
        /// </summary>
        public const string DefaultEmbeddingModel = "nomic-embed-text";

        /// <summary>
        /// Embedding model to use. Defaults to nomic-embed-text (768 dimensions).
        /// </summary>
        public string EmbeddingModel { get; set; } = DefaultEmbeddingModel;
    }
}
