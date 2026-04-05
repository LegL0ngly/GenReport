using GenReport.Infrastructure.InMemory;
using GenReport.Infrastructure.InMemory.Enums;

namespace GenReport.Infrastructure.Models.AI
{
    /// <summary>
    /// Default lightweight model IDs per provider, used when no ModelOverride is set in LlmConfig.
    /// These are the smallest/cheapest models good enough for intent classification.
    /// </summary>
    public static class LightweightModelMap
    {
        private static readonly Dictionary<string, string> ProviderModels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["gemini"]    = "gemini-2.0-flash-lite",
            ["openai"]    = "gpt-4o-mini",
            ["anthropic"] = "claude-3-5-haiku-20241022",
            ["ollama"]    = "llama3.2:1b",
        };

        /// <summary>
        /// Returns the default lightweight model for the given provider.
        /// Falls back to the provider's default model if no lightweight mapping exists.
        /// </summary>
        public static string GetLightweightModel(string provider, string fallbackModel)
        {
            return ProviderModels.TryGetValue(provider.Trim(), out var model)
                ? model
                : fallbackModel;
        }

        /// <summary>
        /// Ollama-aware overload: picks the first locally installed model from the
        /// in-memory store for Ollama, falling back to the hardcoded default (llama3.2:1b)
        /// if the store is empty. For all other providers behaves identically to
        /// <see cref="GetLightweightModel(string, string)"/>.
        /// </summary>
        public static string GetLightweightModel(string provider, string fallbackModel, IInMemoryAiStore aiStore)
        {
            var normalised = provider.Trim().ToLowerInvariant();

            if (normalised == "ollama")
            {
                var installedModels = aiStore.GetModelsForProvider(AiProvider.Ollama);
                if (installedModels.Count > 0)
                    return installedModels[0].ModelId;

                // No Ollama models found in store — use hardcoded fallback
                return ProviderModels.TryGetValue("ollama", out var hardcoded)
                    ? hardcoded
                    : fallbackModel;
            }

            return GetLightweightModel(provider, fallbackModel);
        }
    }
}
