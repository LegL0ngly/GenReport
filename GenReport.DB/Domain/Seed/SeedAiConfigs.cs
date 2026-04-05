using GenReport.DB.Domain.Entities.Core;
using GenReport.DB.Domain.Static;
using Microsoft.EntityFrameworkCore;

namespace GenReport.DB.Domain.Seed
{
    public partial class ApplicationDBContextSeeder
    {

        public async Task SeedAiConfigs()
        {
            var now = DateTime.UtcNow;

            // 1. Seed Intent Classifier for Gemini
            var geminiConnection = await applicationDbContext.AiConnections
                .FirstOrDefaultAsync(c => c.Provider == "gemini");

            if (geminiConnection != null)
            {
                var lightweightModel = geminiConnection.Provider == "gemini" ? "gemini-2.0-flash-lite" : geminiConnection.DefaultModel;

                var hasIntentConfig = await applicationDbContext.AiConfigs
                    .AnyAsync(c => c.Type == AiConfigType.IntentClassifier 
                                && c.AiConnectionId == geminiConnection.Id 
                                && c.ModelId == lightweightModel
                                && c.IsActive);

                if (!hasIntentConfig)
                {
                    var config = new AiConfig
                    {
                        Type = AiConfigType.IntentClassifier,
                        Value = DefaultAiPrompts.IntentClassifier,
                        AiConnectionId = geminiConnection.Id,
                        ModelId = lightweightModel,
                        IsActive = true,
                        Version = 1,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };

                    await applicationDbContext.AiConfigs.AddAsync(config);
                    logger.Information("Seeded IntentClassifier AiConfig for Gemini default connection.");
                }
            }

            // 2. Seed ChatSystemPrompt for all connections
            var allConnections = await applicationDbContext.AiConnections.ToListAsync();
            foreach (var conn in allConnections)
            {
                var promptValue = DefaultAiPrompts.GetChatSystemPrompt(conn.Provider);

                var hasSystemPromptConfig = await applicationDbContext.AiConfigs
                    .AnyAsync(c => c.Type == AiConfigType.ChatSystemPrompt 
                                && c.AiConnectionId == conn.Id 
                                && c.IsActive);

                if (!hasSystemPromptConfig)
                {
                    var config = new AiConfig
                    {
                        Type = AiConfigType.ChatSystemPrompt,
                        Value = promptValue,
                        AiConnectionId = conn.Id,
                        ModelId = null, // System prompt applies globally to the connection
                        IsActive = true,
                        Version = 1,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };

                    await applicationDbContext.AiConfigs.AddAsync(config);
                    logger.Information($"Seeded ChatSystemPrompt AiConfig for {conn.Provider} connection.");
                }
            }

            // Save all newly added configs
            if (applicationDbContext.ChangeTracker.HasChanges())
            {
                await applicationDbContext.SaveChangesAsync();
            }
        }
    }
}
