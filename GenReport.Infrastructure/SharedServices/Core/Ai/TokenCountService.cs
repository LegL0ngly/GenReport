using GenReport.DB.Domain.Interfaces;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.HttpResponse.Core.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using System.Text;

namespace GenReport.Infrastructure.SharedServices.Core.Ai
{
    public class TokenCountService(
        IApplicationDbContext dbContext,
        ILogger<TokenCountService> logger) : ITokenCountService
    {
        // Lazy init so missing data packages don't crash the class on load
        private static readonly Lazy<Tokenizer> _tokenizerLazy = new(
            () => TiktokenTokenizer.CreateForModel("gpt-4o"),
            System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

        public async Task<TokenCountResponse> GetSessionTokenCountAsync(long sessionId, CancellationToken ct = default)
        {
            var session = await dbContext.ChatSessions
                .Include(s => s.Messages)
                .Include(s => s.AiConnection)
                .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

            if (session == null)
            {
                return new TokenCountResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Session not found."
                };
            }

            var aiConnection = session.AiConnection;
            if (aiConnection == null)
            {
                return new TokenCountResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "No AI connection associated with this session."
                };
            }

            var provider = aiConnection.Provider?.ToLowerInvariant() ?? "";
            
            // Build the conversational text
            var fullTextBuilder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(aiConnection.SystemPrompt))
            {
                fullTextBuilder.AppendLine(aiConnection.SystemPrompt);
            }
            
            foreach (var message in session.Messages.OrderBy(m => m.CreatedAt))
            {
                // Typical chat format overhead approximation
                fullTextBuilder.AppendLine($"Role: {message.Role}");
                fullTextBuilder.AppendLine($"Content: {message.Content}");
            }

            int tokenCount = 0;
            string calculationMethod = "Unknown";

            try
            {
                // Try Provider specific APIs if available
                if (provider == "anthropic")
                {
                    // Attempt Anthropic Token Counting API if available.
                    // Fallback to tiktoken for now
                    throw new NotImplementedException("Anthropic native token API not fully wired, falling back to Tiktoken.");
                }
                else if (provider == "gemini")
                {
                    throw new NotImplementedException("Gemini native token API not fully wired, falling back to Tiktoken.");
                }
                else 
                {
                    // openai, ollama, custom -> default to purely local tokenization
                    tokenCount = CountTokensLocal(fullTextBuilder.ToString());
                    calculationMethod = "Local Tiktoken (Primary)";
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Provider specific API for token counting failed or not supported. Falling back to local Tiktoken.");
                tokenCount = CountTokensLocal(fullTextBuilder.ToString());
                calculationMethod = "Local Tiktoken (Fallback)";
            }

            var maxTokens = aiConnection.MaxTokens;
            // Optionally, fallback to standard max tokens if not set
            var limit = maxTokens ?? 128000; 

            return new TokenCountResponse
            {
                IsSuccess = true,
                TotalTokens = tokenCount,
                MaxTokens = limit,
                IsExceeded = tokenCount > limit,
                CalculationMethod = calculationMethod
            };
        }

        private int CountTokensLocal(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return _tokenizerLazy.Value.CountTokens(text);
        }
    }
}
