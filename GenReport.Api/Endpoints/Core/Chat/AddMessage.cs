using FastEndpoints;
using GenReport.DB.Domain.Entities.Core;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Chat;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Chat
{
    public class AddMessage(ApplicationDbContext context, ICurrentUserService currentUserService) : Endpoint<AddMessageRequest, HttpResponse<ChatMessage>>
    {
        public override void Configure()
        {
            Post("/chat/sessions/{id}/messages");
            AllowFileUploads();
        }

        public override async Task HandleAsync(AddMessageRequest req, CancellationToken ct)
        {
            var sessionId = Route<long>("id");
            var userId = currentUserService.LoggedInUserId();

            var session = await context.ChatSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);

            if (session == null)
            {
                await SendAsync(new HttpResponse<ChatMessage>(HttpStatusCode.NotFound, "Chat session not found or access denied.", "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            var uploadedMediaFiles = new List<GenReport.Domain.Entities.Media.MediaFile>();

            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            foreach (var file in Files)
            {
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, ct);
                }

                var mediaUrl = $"/uploads/chat/{fileName}";
                
                var mediaFile = new GenReport.Domain.Entities.Media.MediaFile(
                    storageUrl: mediaUrl,
                    fileName: file.FileName,
                    
                    mimeType: file.ContentType,
                    size: file.Length
                );
                
                context.MediaFiles.Add(mediaFile);
                uploadedMediaFiles.Add(mediaFile);
            }

            if (ValidationFailed)
            {
                await SendErrorsAsync(cancellation: ct);
                return;
            }

            var message = new ChatMessage
            {
                SessionId = sessionId,
                Role = req.Role,
                Content = req.Content,
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
            session.UpdatedAt = DateTime.UtcNow; // Touch session

            await context.SaveChangesAsync(ct);

            await SendAsync(new HttpResponse<ChatMessage>(message, "Message added successfully", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
