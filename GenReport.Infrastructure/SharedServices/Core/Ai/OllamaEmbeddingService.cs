using GenReport.Infrastructure.Configuration;
using GenReport.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace GenReport.Infrastructure.SharedServices.Core.Ai
{
    /// <summary>
    /// Generates 768-dimension vector embeddings using a locally-hosted Ollama instance
    /// and the <c>nomic-embed-text</c> model (or as configured via <see cref="OllamaOptions"/>).
    /// </summary>
    public sealed class OllamaEmbeddingService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaEmbeddingService> logger) : IEmbeddingService
    {
        private const int MaxInputLength = 30_000;

        /// <inheritdoc />
        public async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        {
            if (text.Length > MaxInputLength)
                text = text[..MaxInputLength];

            var opts = options.Value;
            var model = string.IsNullOrWhiteSpace(opts.EmbeddingModel)
                ? OllamaOptions.DefaultEmbeddingModel
                : opts.EmbeddingModel;

            var requestBody = new OllamaEmbeddingRequest(model, text);

            try
            {
                var response = await httpClient.PostAsJsonAsync(
                    "/api/embeddings",
                    requestBody,
                    cancellationToken: ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException(
                        $"Ollama returned HTTP {(int)response.StatusCode}: {error}");
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(ct);

                if (result?.Embedding is null || result.Embedding.Length == 0)
                    throw new InvalidOperationException("Ollama returned an empty embedding vector.");

                return result.Embedding;
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex,
                    "Ollama is unreachable or returned an error at '{BaseUrl}'. Ensure the service is running.",
                    opts.BaseUrl);
                throw new InvalidOperationException(
                    $"Ollama embedding failed — service may be unreachable at '{opts.BaseUrl}'. " +
                    $"Inner: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unexpected error while generating Ollama embedding.");
                throw;
            }
        }

        // ── Request / Response DTOs ──────────────────────────────────────────────────

        private sealed record OllamaEmbeddingRequest(
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("prompt")] string Prompt);

        private sealed class OllamaEmbeddingResponse
        {
            [JsonPropertyName("embedding")]
            public float[]? Embedding { get; set; }
        }
    }
}
