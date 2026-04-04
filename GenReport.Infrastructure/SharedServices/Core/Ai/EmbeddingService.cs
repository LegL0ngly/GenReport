#pragma warning disable SKEXP0010 // Semantic Kernel experimental — OpenAI text embedding
#pragma warning disable CS0618  // ITextEmbeddingGenerationService obsolete — will migrate to IEmbeddingGenerator<string, Embedding<float>> later

using GenReport.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;

namespace GenReport.Infrastructure.SharedServices.Core.Ai
{
    /// <summary>
    /// Generates vector embeddings using the session's AI provider.
    /// Currently supports OpenAI-compatible endpoints (OpenAI, Ollama, custom).
    /// Falls back to null for providers without a text-embedding endpoint.
    /// </summary>
    public sealed class EmbeddingService(ILogger<EmbeddingService> logger) : IEmbeddingService
    {
        // Default embedding model when the session model is a chat model, not an embedding model.
        private const string DefaultOpenAiEmbeddingModel = "text-embedding-3-small";

        public async Task<float[]?> GenerateEmbeddingAsync(
            string text,
            string provider,
            string apiKey,
            string model,
            CancellationToken ct = default)
        {
            // Truncate to avoid exceeding API limits (~8k tokens ≈ 30k chars)
            if (text.Length > 30_000)
                text = text[..30_000];

            var normalised = provider.Trim().ToLowerInvariant();

            // Only OpenAI / OpenAI-compatible providers support text embeddings this way
            // Anthropic and native Gemini HTTP APIs are not yet implemented here
            if (normalised is not ("openai" or "ollama" or "custom"))
            {
                logger.LogWarning("Embedding not supported for provider '{Provider}'. Schema RAG will be skipped.", provider);
                return null;
            }

            // For chat-oriented models passed from the session, use a dedicated embedding model
            var embeddingModel = normalised == "openai"
                ? DefaultOpenAiEmbeddingModel
                : model; // Ollama / custom may expose an embedding model directly

            try
            {
                ITextEmbeddingGenerationService embeddingService = new OpenAITextEmbeddingGenerationService(
                    modelId: embeddingModel,
                    apiKey: apiKey);

                var result = await embeddingService.GenerateEmbeddingAsync(text, cancellationToken: ct);
                return result.ToArray();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate embedding for provider '{Provider}'. Schema RAG will be skipped.", provider);
                return null;
            }
        }
    }
}
