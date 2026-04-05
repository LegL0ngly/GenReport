#pragma warning disable SKEXP0010 // Semantic Kernel experimental — OpenAI text embedding
#pragma warning disable CS0618  // ITextEmbeddingGenerationService obsolete — will migrate to IEmbeddingGenerator<string, Embedding<float>> later

using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;

namespace GenReport.Infrastructure.SharedServices.Core.Ai
{
    /// <summary>
    /// Generates 1536-dimension vector embeddings using the OpenAI text-embedding-3-small model.
    /// Reads the active AI connection's API key from the database.
    /// </summary>
    public sealed class OpenAIEmbeddingService(
        ApplicationDbContext context,
        ICredentialEncryptorFactory encryptorFactory,
        ILogger<OpenAIEmbeddingService> logger) : IEmbeddingService
    {
        private const string DefaultEmbeddingModel = "text-embedding-3-small";
        private const int MaxInputLength = 30_000;

        /// <inheritdoc />
        public async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        {
            if (text.Length > MaxInputLength)
                text = text[..MaxInputLength];

            // Resolve the active OpenAI connection's API key
            var aiConnection = await context.AiConnections
                .AsNoTracking()
                            c.Provider.ToLowerInvariant() == "openai")
                .FirstOrDefaultAsync(ct);

            if (aiConnection == null)
            {
                logger.LogWarning("No active default OpenAI connection found. Skipping OpenAI embedding.");
                return null;
            }

            var apiKeyEncryptor = encryptorFactory.GetEncryptor(CredentialType.ApiKey);
            var decryptedApiKey = apiKeyEncryptor.Decrypt(aiConnection.ApiKey);

            try
            {
                ITextEmbeddingGenerationService embeddingService = new OpenAITextEmbeddingGenerationService(
                    modelId: DefaultEmbeddingModel,
                    apiKey: decryptedApiKey);

                var result = await embeddingService.GenerateEmbeddingAsync(text, cancellationToken: ct);
                return result.ToArray();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate OpenAI embedding.");
                return null;
            }
        }

        /// <summary>
        /// Generates an embedding for the given text using a specific API key and model.
        /// Used by the Schema RAG pipeline where the caller supplies credentials from the session.
        /// </summary>
        public async Task<float[]?> GenerateEmbeddingAsync(
            string text,
            string apiKey,
            string model,
            CancellationToken ct = default)
        {
            if (text.Length > MaxInputLength)
                text = text[..MaxInputLength];

            var embeddingModel = string.IsNullOrWhiteSpace(model) ? DefaultEmbeddingModel : model;

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
                logger.LogError(ex, "Failed to generate OpenAI embedding for model '{Model}'.", embeddingModel);
                return null;
            }
        }
    }
}
