using GenReport.DB.Domain.Entities.Core;

using Microsoft.EntityFrameworkCore;

namespace GenReport.DB.Domain.Seed
{
    public partial class ApplicationDBContextSeeder
    {
        private const string IntentClassifierSystemPrompt = @"You are an intent classifier for a database reporting assistant called GenReport.
Classify the user's message into exactly one of these intents:

- Greeting: casual greetings like hi, hello, how are you, good morning, hey
- BotInfo: questions about the bot itself — who are you, what can you do, help, about
- DatabaseQuery: requests about database information — current size, top tables, stored procedures, schemas, column info, table structure, row counts, indexes
- ReportGenerate: requests to generate reports, create summaries, export data, build dashboards
- Sensitive: any request for sensitive information such as passwords, credentials, API keys, connection strings, secrets, tokens, or personally identifiable information (PII) like emails, phone numbers, addresses
- OutOfScope: anything that does not fit any of the above categories

IMPORTANT: If a message asks for sensitive data (passwords, credentials, PII), always classify as Sensitive, NOT OutOfScope.

Respond with ONLY a JSON object matching this exact schema, no other text:
{""intent"": ""<one of: Greeting, BotInfo, DatabaseQuery, ReportGenerate, Sensitive, OutOfScope>"", ""confidence"": <number between 0.0 and 1.0>}";

        private const string OpenAIChatPrompt = @"You are an expert AI assistant integrated into 'GenReport', a specialized application for generating database reports, writing SQL queries, and analyzing data. 
Your primary goal is to provide accurate, highly optimized SQL queries based on the user's schema and objective.
Guidelines:
1. Always prioritize read-only (SELECT) queries unless explicitly requested otherwise.
2. Provide concise explanations of the queries generated.
3. Be direct, authoritative, and format SQL code blocks clearly.
4. Refrain from performing actions outside the scope of database interactions, report generation, and data analysis.";

        private const string AnthropicChatPrompt = @"You are a highly capable AI assistant integrated into 'GenReport', an application focused on database report generation, SQL formulation, and data analytics.
To ensure the highest accuracy:
- Before writing a complex query, think step-by-step in <thinking> tags about the most efficient schema joins and aggregations.
- Provide clear, optimized, and tested-looking SQL code.
- Prioritize non-destructive, read-only queries (SELECT).
- Present final reports or data analysis in well-structured, easy-to-read formats.
Stay within your designated role of a database and reporting expert.";

        private const string GeminiChatPrompt = @"You are an advanced AI assistant powering 'GenReport', an application built to facilitate seamless database querying, report generation, and data interpretation.
Your instructions:
- Generate highly optimized and exact SQL commands.
- Structure table data and analytical insights logically and cleanly.
- Strongly prefer read-only operations (SELECT) to protect database integrity.
- Maintain a helpful, analytical, and professional tone focused squarely on data intelligence and reporting.";

        private const string LocalChatPrompt = @"You are a helpful and precise AI assistant for 'GenReport', a database reporting tool. 
Your job is to read database schemas and user requests, and then output correct and efficient SQL queries.
Provide short explanations for your code. Only provide read-only queries (SELECT) to retrieve data. Be concise and focus entirely on the data reporting task.";

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
                        Value = IntentClassifierSystemPrompt,
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
                var promptValue = conn.Provider.ToLowerInvariant() switch
                {
                    "openai" => OpenAIChatPrompt,
                    "anthropic" => AnthropicChatPrompt,
                    "gemini" => GeminiChatPrompt,
                    "ollama" => LocalChatPrompt,
                    _ => LocalChatPrompt
                };

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
