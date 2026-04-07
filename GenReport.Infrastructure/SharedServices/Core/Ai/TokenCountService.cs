using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.HttpResponse.Core.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace GenReport.Infrastructure.SharedServices.Core.Ai
{
    public class TokenCountService(
        ApplicationDbContext dbContext,
        ILogger<TokenCountService> logger,
        IHttpClientFactory httpClientFactory) : ITokenCountService
    {
        public async Task<TokenCountResponse> GetSessionTokenCountAsync(long sessionId, string? updatedSystemPrompt = null, CancellationToken ct = default)
        {
            var session = await dbContext.ChatSessions
                .Include(s => s.Messages)
                .Include(s => s.AiConnection)
                .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

            if (session == null)
                return new TokenCountResponse { IsSuccess = false, ErrorMessage = "Session not found." };

            var aiConnection = session.AiConnection;
            if (aiConnection == null)
                return new TokenCountResponse
                    { IsSuccess = false, ErrorMessage = "No AI connection associated with this session." };

            if (string.IsNullOrWhiteSpace(aiConnection.ApiKey))
                return new TokenCountResponse
                    { IsSuccess = false, ErrorMessage = "No API key configured for this AI connection." };

            var modelId = string.IsNullOrWhiteSpace(session.ModelId)
                ? aiConnection.DefaultModel
                : session.ModelId;

            if (string.IsNullOrWhiteSpace(modelId))
                return new TokenCountResponse
                    { IsSuccess = false, ErrorMessage = "No model ID configured for this session." };

            var provider = aiConnection.Provider?.ToLowerInvariant() ?? "";

            // Build ordered message list
            var orderedMessages = session.Messages
                .OrderBy(m => m.CreatedAt)
                .ToList();

            int tokenCount = 0;
            string calculationMethod = "Unknown";
            string systemPromptToUse = string.IsNullOrWhiteSpace(updatedSystemPrompt) 
                                        ? aiConnection.SystemPrompt 
                                        : updatedSystemPrompt;

            try
            {
                tokenCount = provider switch
                {
                    "anthropic" => await CountTokensAnthropicAsync(
                        aiConnection.ApiKey,
                        modelId,
                        systemPromptToUse,
                        orderedMessages,
                        ct),

                    "gemini" => await CountTokensGeminiAsync(
                        aiConnection.ApiKey,
                        modelId,
                        systemPromptToUse,
                        orderedMessages,
                        ct),

                    "openai" => await CountTokensOpenAiAsync(
                        aiConnection.ApiKey,
                        modelId,
                        systemPromptToUse,
                        orderedMessages,
                        ct),

                    // ollama, custom, unknown → local
                    _ => CountTokensLocal(BuildFullText(systemPromptToUse, orderedMessages))
                };

                calculationMethod = provider switch
                {
                    "anthropic" => "Anthropic Token Count API (Native)",
                    "gemini" => "Gemini countTokens API (Native)",
                    "openai" => "OpenAI Local Estimation",
                    _ => "Local Estimation (Primary)"
                };
            }
            catch (Exception ex)
            {
                var hasLocalLlmConfigured = await HasLocalLlmConfiguredAsync(ct);
                if (!hasLocalLlmConfigured)
                {
                    logger.LogWarning(ex,
                        "Provider-specific token count failed for provider '{Provider}'. Skipping token count because no local LLM is configured.",
                        provider);
                    tokenCount = 0;
                    calculationMethod = "Skipped (Provider API Failed, No Local LLM Configured)";
                }
                else
                {
                    logger.LogWarning(ex,
                        "Provider-specific token count failed for provider '{Provider}'. Falling back to local estimation.",
                        provider);
                    tokenCount = CountTokensLocal(BuildFullText(systemPromptToUse, orderedMessages));
                    calculationMethod = "Local Estimation (Fallback)";
                }
            }

            var limit = aiConnection.MaxTokens ?? 128000;

            return new TokenCountResponse
            {
                IsSuccess = true,
                TotalTokens = tokenCount,
                MaxTokens = limit,
                IsExceeded = tokenCount > limit,
                CalculationMethod = calculationMethod
            };
        }

        // -------------------------------------------------------------------------
        // Anthropic — native token-counting endpoint
        // POST https://api.anthropic.com/v1/messages/count_tokens
        // -------------------------------------------------------------------------
        private async Task<int> CountTokensAnthropicAsync(
            string apiKey,
            string modelId,
            string? systemPrompt,
            IEnumerable<dynamic> messages,
            CancellationToken ct)
        {
            var client = httpClientFactory.CreateClient("Anthropic");
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
            client.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");

            var payload = new
            {
                model = modelId,
                system = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
                messages = messages.Select(m => new
                {
                    role = NormalizeRole(m.Role, "anthropic"),
                    content = (string)m.Content
                }).ToArray()
            };

            var response = await client.PostAsJsonAsync("/v1/messages/count_tokens", payload, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<AnthropicTokenCountResponse>(cancellationToken: ct)
                       ?? throw new InvalidOperationException("Empty response from Anthropic token count API.");

            return json.InputTokens;
        }

        // -------------------------------------------------------------------------
        // Gemini — native countTokens endpoint
        // POST https://generativelanguage.googleapis.com/v1beta/models/{model}:countTokens
        // -------------------------------------------------------------------------
        private async Task<int> CountTokensGeminiAsync(
            string apiKey,
            string modelId,
            string? systemPrompt,
            IEnumerable<dynamic> messages,
            CancellationToken ct)
        {
            var client = httpClientFactory.CreateClient("Gemini");
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:countTokens?key={apiKey}";

            var contents = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                contents.Add(new
                {
                    role = "user",
                    parts = new[] { new { text = $"[System]: {systemPrompt}" } }
                });
            }

            foreach (var m in messages)
            {
                contents.Add(new
                {
                    role = NormalizeRole(m.Role, "gemini"),
                    parts = new[] { new { text = (string)m.Content } }
                });
            }

            var payload = new { contents };

            var response = await client.PostAsJsonAsync(url, payload, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<GeminiTokenCountResponse>(cancellationToken: ct)
                       ?? throw new InvalidOperationException("Empty response from Gemini countTokens API.");

            return json.TotalTokens;
        }

        private Task<int> CountTokensOpenAiAsync(
            string apiKey,
            string modelId,
            string? systemPrompt,
            IEnumerable<dynamic> messages,
            CancellationToken ct)
        {
            var fullText = BuildFullText(systemPrompt, messages);
            return Task.FromResult(CountTokensLocal(fullText));
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static string BuildFullText(string? systemPrompt, IEnumerable<dynamic> messages)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                sb.AppendLine(systemPrompt);

            foreach (var m in messages)
            {
                sb.AppendLine($"Role: {m.Role}");
                sb.AppendLine($"Content: {m.Content}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Normalises role strings to what each provider expects.
        /// Anthropic: "user" | "assistant"
        /// Gemini:    "user" | "model"
        /// </summary>
        private static string NormalizeRole(string role, string provider)
        {
            var normalised = role?.ToLowerInvariant() switch
            {
                "assistant" or "bot" or "ai" => "assistant",
                _ => "user"
            };

            if (provider == "gemini" && normalised == "assistant")
                return "model";

            return normalised;
        }

        private int CountTokensLocal(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text
                .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Length;
        }

        private Task<bool> HasLocalLlmConfiguredAsync(CancellationToken ct)
        {
            return dbContext.AiConnections.AnyAsync(
                a => a.IsActive && (a.Provider.ToLower() == "ollama" || a.Provider.ToLower() == "custom"),
                ct);
        }

        // -------------------------------------------------------------------------
        // Private response DTOs
        // -------------------------------------------------------------------------

        private sealed class AnthropicTokenCountResponse
        {
            [JsonPropertyName("input_tokens")] public int InputTokens { get; init; }
        }

        private sealed class GeminiTokenCountResponse
        {
            [JsonPropertyName("totalTokens")] public int TotalTokens { get; init; }
        }
    }
}
