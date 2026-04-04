using FastEndpoints;
using GenReport.DB.Domain.Entities.Core;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.AI;
using GenReport.Infrastructure.Models.HttpRequests.Core.Chat;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Infrastructure.Security.Encryption;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Net;
using System.Text;

namespace GenReport.Api.Endpoints.Core.Chat
{
    public class AddMessage(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIntentClassifierService intentClassifierService,
        ICredentialEncryptorFactory encryptorFactory,
        ITokenCountService tokenCountService,
        ISchemaSearchService schemaSearchService,
        IChatCompletionFactory chatCompletionFactory) : Endpoint<AddMessageRequest>
    {
        public override void Configure()
        {
            Post("/chat/sessions/messages");
        }

        public override async Task HandleAsync(AddMessageRequest req, CancellationToken ct)
        {
            if (!long.TryParse(req.SessionId, out var sessionId))
            {
                await SendAsync(
                    new HttpResponse<ChatMessage>(HttpStatusCode.BadRequest, "Missing or invalid session ID.",
                        "ERR_BAD_REQUEST", []), cancellation: ct);
                return;
            }

            var userId = currentUserService.LoggedInUserId();

            var session = await context.ChatSessions
                .Include(s => s.AiConnection)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);

            if (session == null)
            {
                await SendAsync(
                    new HttpResponse<ChatMessage>(HttpStatusCode.NotFound, "Chat session not found or access denied.",
                        "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            // Extract the user message content from the Next.js AI SDK payload
            var lastUserMessage = req.Messages.LastOrDefault(m => m.Role == "user");
            if (lastUserMessage == null)
            {
                await SendAsync(
                    new HttpResponse<ChatMessage>(HttpStatusCode.BadRequest, "No user message found to process.",
                        "ERR_BAD_REQUEST", []), cancellation: ct);
                return;
            }

            var role = lastUserMessage.Role;
            var content = lastUserMessage.Content ?? lastUserMessage.Parts?.FirstOrDefault()?.Text ?? string.Empty;

            if (ValidationFailed)
            {
                await SendErrorsAsync(cancellation: ct);
                return;
            }

            // ── Intent Classification ────────────────────────────────────────────────
            string? classifiedIntentName = null;
            ChatIntent? intentEnum = null;

            if (session.AiConnection != null)
            {
                var apiKeyEncryptor = encryptorFactory.GetEncryptor(CredentialType.ApiKey);
                var decryptedApiKey = apiKeyEncryptor.Decrypt(session.AiConnection.ApiKey);

                var classificationResult = await intentClassifierService.ClassifyAsync(
                    content,
                    session.AiConnection.Provider,
                    decryptedApiKey,
                    session.AiConnection.DefaultModel,
                    ct);

                intentEnum = classificationResult.Intent;
                classifiedIntentName = classificationResult.Intent.ToString();
            }

            // ── Handle attachments ────────────────────────────────────────────────────
            var uploadedMediaFiles = new List<GenReport.Domain.Entities.Media.MediaFile>();

            foreach (var attachedFile in req.Attachments)
            {
                var mediaFile = new GenReport.Domain.Entities.Media.MediaFile(
                    storageUrl: attachedFile.Url,
                    fileName: attachedFile.FileName,
                    mimeType: attachedFile.ContentType,
                    size: attachedFile.Size
                );

                context.MediaFiles.Add(mediaFile);
                uploadedMediaFiles.Add(mediaFile);
            }

            // ── Persist user message ─────────────────────────────────────────────────
            var message = new ChatMessage
            {
                SessionId = sessionId,
                Role = role,
                Content = content,
                Intent = classifiedIntentName,
                Attachments = new List<MessageAttachment>()
            };

            foreach (var mediaFile in uploadedMediaFiles)
            {
                message.Attachments.Add(new MessageAttachment
                {
                    Message = message,
                    MediaFile = mediaFile
                });
            }

            context.ChatMessages.Add(message);
            session.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            // ── Token count check ────────────────────────────────────────────────────
            var tokenCountResponse = await tokenCountService.GetSessionTokenCountAsync(sessionId, ct);
            if (tokenCountResponse.IsSuccess && tokenCountResponse.IsExceeded)
            {
                await SendAsync(
                    new HttpResponse<ChatMessage>(
                        HttpStatusCode.BadRequest,
                        "Context window exceeded. Please start a new chat.",
                        "ERR_CONTEXT_WINDOW_EXCEEDED",
                        []),
                    cancellation: ct);
                return;
            }

            // ── Out-of-scope / sensitive short-circuit ──────────────────────────────
            if (intentEnum == ChatIntent.OutOfScope || intentEnum == ChatIntent.Sensitive)
            {
                var shortCircuitContent = intentEnum == ChatIntent.Sensitive
                    ? "I cannot process requests involving sensitive information, passwords, credentials, or personal data."
                    : "I'm sorry, I can only help with database queries, report generation, or general questions about my capabilities.";

                await StreamWordsAsync(shortCircuitContent, ct);

                var shortCircuitMessage = new ChatMessage
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Content = shortCircuitContent,
                    Attachments = new List<MessageAttachment>()
                };
                context.ChatMessages.Add(shortCircuitMessage);
                await context.SaveChangesAsync(CancellationToken.None);
                return;
            }

            // ── No AI connection configured ───────────────────────────────────────────
            if (session.AiConnection == null)
            {
                await StreamWordsAsync("No AI connection is configured for this session.", ct);
                return;
            }

            var apiKeyEncryptorFinal = encryptorFactory.GetEncryptor(CredentialType.ApiKey);
            var decryptedApiKeyFinal = apiKeyEncryptorFinal.Decrypt(session.AiConnection.ApiKey);

            // ── Load base system prompt from AiConfig ────────────────────────────────
            var systemConfig = await context.AiConfigs
                .AsNoTracking()
                .Where(c => c.AiConnectionId == session.AiConnection.Id
                            && c.Type == AiConfigType.ChatSystemPrompt
                            && c.IsActive)
                .OrderByDescending(c => c.Version)
                .FirstOrDefaultAsync(ct);

            var baseSystemPrompt = systemConfig?.Value ?? string.Empty;

            // ── Schema RAG: search relevant schema for this session ──────────────────
            var schemaContext = new StringBuilder();
            if (session.DatabaseId.HasValue && !string.IsNullOrWhiteSpace(content))
            {
                // Build the search query from entire conversation for better recall
                var allUserMessages = await context.ChatMessages
                    .AsNoTracking()
                    .Where(m => m.SessionId == sessionId && m.Role == "user")
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => m.Content)
                    .ToListAsync(ct);

                var combinedQuery = string.Join(" ", allUserMessages);

                var relevantSchema = await schemaSearchService.SearchAsync(
                    combinedQuery,
                    session.DatabaseId.Value,
                    session.AiConnection.Provider,
                    decryptedApiKeyFinal,
                    session.AiConnection.DefaultModel,
                    ct);

                if (relevantSchema.Count > 0)
                {
                    schemaContext.AppendLine("\n\n--- Relevant Database Schema ---");
                    foreach (var obj in relevantSchema)
                    {
                        schemaContext.AppendLine($"\n-- {obj.Type.ToUpperInvariant()}: {obj.Name}");
                        schemaContext.AppendLine(obj.FullSchema);
                    }
                }
            }

            var dynamicSystemPrompt = baseSystemPrompt + schemaContext.ToString();

            // ── Build ChatHistory for LLM ─────────────────────────────────────────────
            // Load all persisted messages for this session (excluding system messages — we inject ours dynamically)
            var dbMessages = await context.ChatMessages
                .AsNoTracking()
                .Where(m => m.SessionId == sessionId && m.Role != "system")
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(ct);

            var chatHistory = new ChatHistory();

            // Always inject the (possibly schema-augmented) system prompt in-memory
            if (!string.IsNullOrWhiteSpace(dynamicSystemPrompt))
                chatHistory.AddSystemMessage(dynamicSystemPrompt);

            foreach (var dbMsg in dbMessages)
            {
                if (dbMsg.Role == "user")
                    chatHistory.AddUserMessage(dbMsg.Content);
                else if (dbMsg.Role == "assistant")
                    chatHistory.AddAssistantMessage(dbMsg.Content);
            }

            // ── Call the LLM and stream ───────────────────────────────────────────────
            var chatService = chatCompletionFactory.Create(
                session.AiConnection.Provider,
                decryptedApiKeyFinal,
                req.ModelId ?? session.ModelId ?? session.AiConnection.DefaultModel);

            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.Headers.CacheControl = "no-cache";
            HttpContext.Response.Headers.Connection = "keep-alive";
            HttpContext.Response.Headers["X-Accel-Buffering"] = "no";
            HttpContext.Response.ContentType = "text/event-stream; charset=utf-8";
            await HttpContext.Response.StartAsync(CancellationToken.None);

            var actualStreamedContent = new StringBuilder();

            try
            {
                static string ToSseDataLine(string json) => $"data: {json}\n\n";

                async Task WriteSseAsync(object payload)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(payload);
                    await HttpContext.Response.WriteAsync(ToSseDataLine(json), CancellationToken.None);
                    await HttpContext.Response.Body.FlushAsync(CancellationToken.None);
                }

                var assistantMessageId = Guid.NewGuid().ToString("N");
                var textPartId = Guid.NewGuid().ToString("N");

                await WriteSseAsync(new { type = "start", messageId = assistantMessageId });
                await WriteSseAsync(new { type = "text-start", id = textPartId });

                await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(chatHistory, cancellationToken: CancellationToken.None))
                {
                    if (ct.IsCancellationRequested) break;

                    var delta = chunk.Content ?? string.Empty;
                    if (string.IsNullOrEmpty(delta)) continue;

                    actualStreamedContent.Append(delta);

                    await WriteSseAsync(new
                    {
                        type = "text-delta",
                        id = textPartId,
                        delta
                    });
                }

                await WriteSseAsync(new { type = "text-end", id = textPartId });
                await WriteSseAsync(new { type = "finish", finishReason = "stop" });
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is IOException)
            {
                // Client disconnected — save whatever was streamed
            }

            // ── Persist assistant message ──────────────────────────────────────────────
            var assistantMessage = new ChatMessage
            {
                SessionId = sessionId,
                Role = "assistant",
                Content = actualStreamedContent.ToString().TrimEnd(),
                Attachments = new List<MessageAttachment>()
            };

            context.ChatMessages.Add(assistantMessage);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        /// <summary>Streams a static text response word-by-word as SSE then sets up the response.</summary>
        private async Task StreamWordsAsync(string text, CancellationToken ct)
        {
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.Headers.CacheControl = "no-cache";
            HttpContext.Response.Headers.Connection = "keep-alive";
            HttpContext.Response.Headers["X-Accel-Buffering"] = "no";
            HttpContext.Response.ContentType = "text/event-stream; charset=utf-8";
            await HttpContext.Response.StartAsync(CancellationToken.None);

            static string ToSseDataLine(string json) => $"data: {json}\n\n";
            async Task WriteSseAsync(object payload)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                await HttpContext.Response.WriteAsync(ToSseDataLine(json), CancellationToken.None);
                await HttpContext.Response.Body.FlushAsync(CancellationToken.None);
            }

            var msgId = Guid.NewGuid().ToString("N");
            var partId = Guid.NewGuid().ToString("N");

            await WriteSseAsync(new { type = "start", messageId = msgId });
            await WriteSseAsync(new { type = "text-start", id = partId });

            var words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (ct.IsCancellationRequested) break;
                await Task.Delay(80, CancellationToken.None);
                var chunk = words[i] + (i < words.Length - 1 ? " " : "");
                await WriteSseAsync(new { type = "text-delta", id = partId, delta = chunk });
            }

            await WriteSseAsync(new { type = "text-end", id = partId });
            await WriteSseAsync(new { type = "finish", finishReason = "stop" });
        }
    }
}
