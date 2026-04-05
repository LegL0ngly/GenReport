#pragma warning disable SKEXP0070 // Semantic Kernel experimental API — Google AI connector

using System.Text.Json;
using GenReport.DB.Domain.Entities.Core;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.AI;
using GenReport.DB.Domain.Static;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;

namespace GenReport.Infrastructure.SharedServices.Core.Ai
{
    /// <summary>
    /// Classifies user messages by sending them to a lightweight LLM.
    /// Loads instructions from the active <see cref="AiConfig"/> and combines them with the user text.
    /// </summary>
    public sealed class IntentClassifierService(
        IChatCompletionFactory chatCompletionFactory,
        ApplicationDbContext context,
        ILogger<IntentClassifierService> logger) : IIntentClassifierService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<IntentClassificationResult> ClassifyAsync(
            string userMessage,
            string provider,
            string apiKey,
            string defaultModel,
            CancellationToken ct = default)
        {
            try
            {
                // Note: The caller (AddMessage endpoint) should ideally pass the connection ID
                // so we can query strictly by `AiConnectionId`. Since the interface doesn't
                // have connectionId yet, we fetch the connection from the DB using matching Provider.
                // Alternatively, we should change the interface to accept AiConnection object directly or its ID.
                // Assuming we use the active AiConnection matching the given provider.
                
                var connection = await context.AiConnections
                    .FirstOrDefaultAsync(c => c.Provider == provider && c.IsActive, ct);

                if (connection == null)
                {
                    logger.LogWarning("No active AiConnection found for provider '{Provider}'. Defaulting to OutOfScope.", provider);
                    return FallbackResult();
                }

                var lightweightModel = LightweightModelMap.GetLightweightModel(provider, connection.DefaultModel);

                // Check for a specific config matching this connection + model
                var config = await context.AiConfigs
                    .AsNoTracking()
                    .Where(c => c.Type == AiConfigType.IntentClassifier
                             && c.AiConnectionId == connection.Id
                             && c.IsActive)
                    // Prefer the one matching our lightweight model, fallback to a catch-all (ModelId null)
                    .OrderByDescending(c => c.ModelId == lightweightModel ? 1 : 0)
                    .FirstOrDefaultAsync(ct);

                // Fall back to the built-in default prompt if no config row exists
                var promptValue = config?.Value ?? DefaultAiPrompts.IntentClassifier;

                if (config == null)
                    logger.LogWarning("No active IntentClassifier AiConfig found for connection {Id}. Using built-in default prompt.", connection.Id);

                // Create the chat completion service via factory
                var chatService = chatCompletionFactory.Create(provider, apiKey, lightweightModel);

                // Build combined prompt and classify
                var combinedPrompt = $"{promptValue}\n\nUser Message:\n{userMessage}";
                var chatHistory = new ChatHistory();
                chatHistory.AddUserMessage(combinedPrompt);

                // Get execution settings for controlled JSON output
                var executionSettings = GetExecutionSettings(provider);

                // Call the LLM
                var response = await chatService.GetChatMessageContentAsync(
                    chatHistory, executionSettings, cancellationToken: ct);

                var responseText = response?.Content?.Trim();

                if (string.IsNullOrWhiteSpace(responseText))
                {
                    logger.LogWarning("Intent classifier returned empty response. Defaulting to OutOfScope.");
                    return FallbackResult();
                }

                // Parse the JSON response
                return ParseClassificationResult(responseText);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Intent classification failed for message: {Message}. Defaulting to OutOfScope.",
                    userMessage.Length > 100 ? userMessage[..100] + "..." : userMessage);
                return FallbackResult();
            }
        }

        private static IntentClassificationResult FallbackResult() => new()
        {
            Intent = ChatIntent.OutOfScope,
            Confidence = 0.0
        };

        /// <summary>
        /// Returns provider-specific execution settings for controlled JSON output.
        /// </summary>
        private static PromptExecutionSettings? GetExecutionSettings(string provider)
        {
            var normalisedProvider = provider.Trim().ToLowerInvariant();

            return normalisedProvider switch
            {
                "openai" => new OpenAIPromptExecutionSettings
                {
                    ResponseFormat = typeof(IntentClassificationResult),
                    Temperature = 0.1,
                    MaxTokens = 100
                },

                "gemini" => new GeminiPromptExecutionSettings
                {
                    ResponseMimeType = "application/json",
                    ResponseSchema = typeof(IntentClassificationResult),
                    Temperature = 0.1,
                    MaxTokens = 100
                },

                // Anthropic + Ollama + Custom: rely on combined prompt instructions to produce JSON
                _ => null
            };
        }

        private IntentClassificationResult ParseClassificationResult(string responseText)
        {
            var cleaned = responseText;
            if (cleaned.StartsWith("```"))
            {
                var firstNewline = cleaned.IndexOf('\n');
                if (firstNewline >= 0)
                    cleaned = cleaned[(firstNewline + 1)..];

                if (cleaned.EndsWith("```"))
                    cleaned = cleaned[..^3];

                cleaned = cleaned.Trim();
            }

            try
            {
                var result = JsonSerializer.Deserialize<IntentClassificationResult>(cleaned, JsonOptions);

                if (result != null)
                {
                    logger.LogInformation("Intent classified: {Intent} (confidence: {Confidence:F2})",
                        result.Intent, result.Confidence);
                    return result;
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize intent classification response: {Response}", cleaned);
            }

            foreach (var intent in Enum.GetValues<ChatIntent>())
            {
                if (cleaned.Contains(intent.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return new IntentClassificationResult { Intent = intent, Confidence = 0.5 };
                }
            }

            return FallbackResult();
        }
    }
}
