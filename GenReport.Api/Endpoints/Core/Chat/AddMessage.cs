using FastEndpoints;
using GenReport.DB.Domain.Entities.Core;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.AI;
using GenReport.Infrastructure.Models.HttpRequests.Core.Chat;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Infrastructure.Security.Encryption;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Runtime.CompilerServices; // Required for EnumeratorCancellation

namespace GenReport.Api.Endpoints.Core.Chat
{
    public class AddMessage(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IIntentClassifierService intentClassifierService,
        ICredentialEncryptorFactory encryptorFactory) : Endpoint<AddMessageRequest>
    {
        public override void Configure()
        {
            Post("/chat/sessions/messages");
        }

        public override async Task HandleAsync(AddMessageRequest req, CancellationToken ct)
        {
            // The Next.js AI SDK frontend should pass the actual database session ID
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

            // Extract the actual message content from the Next.js AI SDK request payload
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

            // Intent Classification
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

            // Save User Message
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


            string assistantContent;
            
            // Calculate approximate context length in characters (rough estimation for tokens)
            var currentContextLength = await context.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .SumAsync(m => m.Content.Length, cancellationToken: ct);
            
            currentContextLength += content.Length;

            int maxContextTokens = session.AiConnection?.Provider.ToLower() switch
            {
                "openai" => 128000,
                "anthropic" => 200000,
                "gemini" => 1000000,
                _ => 256000
            };
            
            // Assuming average of 4 chars per token
            int maxContextChars = maxContextTokens * 4;

            if (currentContextLength > maxContextChars)
            {
                assistantContent = "Context length exceeded. Please open a new chat to continue.";
            }
            else if (intentEnum == ChatIntent.OutOfScope || intentEnum == ChatIntent.Sensitive)
            {
                assistantContent = intentEnum == ChatIntent.Sensitive
                    ? "I cannot process requests involving sensitive information, passwords, credentials, or personal data."
                    : "I'm sorry, I can only help with database queries, report generation, or general questions about my capabilities.";
            }
            else
            {
                assistantContent = "hey how're you doing";
            }
  
            var words = assistantContent.Split(' ');
            var actualStreamedContent = new System.Text.StringBuilder();

            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.Headers.CacheControl = "no-cache";
            HttpContext.Response.Headers.Connection = "keep-alive";
            HttpContext.Response.Headers["X-Accel-Buffering"] = "no";
            HttpContext.Response.ContentType = "text/event-stream; charset=utf-8";
            await HttpContext.Response.StartAsync(CancellationToken.None);

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

                await WriteSseAsync(new
                {
                    type = "start",
                    messageId = assistantMessageId
                });

                await WriteSseAsync(new
                {
                    type = "text-start",
                    id = textPartId
                });

                for (int i = 0; i < words.Length; i++)
                {
                    if (ct.IsCancellationRequested) break; // check manually instead

                    await Task.Delay(200, CancellationToken.None); // ← don't pass ct here

                    var chunk = words[i] + (i < words.Length - 1 ? " " : "");
                    actualStreamedContent.Append(chunk);

                    await WriteSseAsync(new
                    {
                        type = "text-delta",
                        id = textPartId,
                        delta = chunk
                    });
                }

                await WriteSseAsync(new
                {
                    type = "text-end",
                    id = textPartId
                });

                await WriteSseAsync(new
                {
                    type = "finish",
                    finishReason = "stop"
                });
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is IOException)
            {
                // Client disconnected — save what we have
            }

            // Save the assistant message to the DB with whatever was streamed so far
            var assistantMessage = new ChatMessage
            {
                SessionId = sessionId,
                Role = "assistant",
                Content = actualStreamedContent.ToString().TrimEnd(),
                Attachments = new List<MessageAttachment>()
            };

            context.ChatMessages.Add(assistantMessage);
            // Use CancellationToken.None to ensure it saves even if the request was cancelled
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }
}
