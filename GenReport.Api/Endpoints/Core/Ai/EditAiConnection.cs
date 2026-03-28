using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Ai;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Infrastructure.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Ai
{
    public class EditAiConnectionRequest_Route
    {
        public long Id { get; set; }
    }

    /// <summary>PUT /ai/connections/{id} — partial update; re-encrypts API key if provided.</summary>
    public class EditAiConnection(
        ApplicationDbContext context,
        ILogger<EditAiConnection> logger,
        ICredentialEncryptorFactory encryptorFactory)
        : Endpoint<EditAiConnectionRequest, HttpResponse<string>>
    {
        public override void Configure()
        {
            Put("/ai/connections/{id}");
        }

        public override async Task HandleAsync(EditAiConnectionRequest req, CancellationToken ct)
        {
            var id = Route<long>("id");
            var connection = await context.AiConnections.FirstOrDefaultAsync(c => c.Id == id, ct);

            if (connection == null)
            {
                await SendAsync(new HttpResponse<string>(HttpStatusCode.NotFound, "AI connection not found.", "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            if (!string.IsNullOrEmpty(req.ApiKey))
                connection.ApiKey = encryptorFactory.GetEncryptor(CredentialType.ApiKey).Encrypt(req.ApiKey);

            if (req.DefaultModel != null)          connection.DefaultModel          = req.DefaultModel;
            if (req.SystemPrompt != null)          connection.SystemPrompt          = req.SystemPrompt;
            if (req.Temperature.HasValue)          connection.Temperature           = req.Temperature;
            if (req.MaxTokens.HasValue)            connection.MaxTokens             = req.MaxTokens;
            if (req.RateLimitRpm.HasValue)         connection.RateLimitRpm          = req.RateLimitRpm;
            if (req.RateLimitTpm.HasValue)         connection.RateLimitTpm          = req.RateLimitTpm;
            if (req.CostPer1kInputTokens.HasValue) connection.CostPer1kInputTokens  = req.CostPer1kInputTokens;
            if (req.CostPer1kOutputTokens.HasValue)connection.CostPer1kOutputTokens = req.CostPer1kOutputTokens;
            if (req.IsActive.HasValue)             connection.IsActive              = req.IsActive.Value;

            // If promoting this connection to default, unset any other default for the same provider.
            if (req.IsDefault.HasValue)
            {
                if (req.IsDefault.Value && !connection.IsDefault)
                {
                    var otherDefaults = await context.AiConnections
                        .Where(c => c.Provider == connection.Provider && c.IsDefault && c.Id != connection.Id)
                        .ToListAsync(ct);

                    foreach (var other in otherDefaults)
                        other.IsDefault = false;
                }
                connection.IsDefault = req.IsDefault.Value;
            }

            connection.UpdatedAt = DateTime.UtcNow;

            context.AiConnections.Update(connection);
            await context.SaveChangesAsync(ct);

            await SendAsync(new HttpResponse<string>("Success", "AI connection updated.", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
