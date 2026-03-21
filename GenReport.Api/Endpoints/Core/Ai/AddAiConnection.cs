using FastEndpoints;
using GenReport.DB.Domain.Entities.Core;
using GenReport.DB.Domain.Enums;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Ai;
using GenReport.Infrastructure.Models.HttpResponse.Core.Ai;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Infrastructure.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Ai
{
    /// <summary>
    /// POST /ai/connections — creates a new AI connection and seeds the 3 default model endpoints.
    /// </summary>
    public class AddAiConnection(
        ApplicationDbContext context,
        ILogger<AddAiConnection> logger,
        ICredentialEncryptorFactory encryptorFactory)
        : Endpoint<AddAiConnectionRequest, HttpResponse<AiConnectionResponse>>
    {
        // Default endpoints seeded for every new AI connection.
        private static readonly (AiEndpointType Type, string Path, string Method)[] DefaultEndpoints =
        [
            (AiEndpointType.Chat,   "/v1/chat/completions", "POST"),
            (AiEndpointType.Models, "/v1/models",           "GET"),
            (AiEndpointType.Quota,  "/v1/usage",            "GET"),
        ];

        public override void Configure()
        {
            Post("/ai/connections");
        }

        public override async Task HandleAsync(AddAiConnectionRequest req, CancellationToken ct)
        {
            // Prevent duplicate providers (one config per provider)
            var exists = await context.AiConnections
                .AnyAsync(c => c.Provider == req.Provider, ct);

            if (exists)
            {
                await SendAsync(new HttpResponse<AiConnectionResponse>(
                    HttpStatusCode.Conflict,
                    $"An AI connection for provider '{req.Provider}' already exists.",
                    "ERR_CONFLICT", []), cancellation: ct);
                return;
            }

            var encryptedKey = encryptorFactory
                .GetEncryptor(CredentialType.ApiKey)
                .Encrypt(req.ApiKey);

            var connection = new AiConnection
            {
                Provider              = req.Provider,
                ApiKey                = encryptedKey,
                DefaultModel          = req.DefaultModel,
                SystemPrompt          = req.SystemPrompt,
                Temperature           = req.Temperature,
                MaxTokens             = req.MaxTokens,
                RateLimitRpm          = req.RateLimitRpm,
                RateLimitTpm          = req.RateLimitTpm,
                CostPer1kInputTokens  = req.CostPer1kInputTokens,
                CostPer1kOutputTokens = req.CostPer1kOutputTokens,
                IsActive              = req.IsActive,
                CreatedAt             = DateTime.UtcNow,
                UpdatedAt             = DateTime.UtcNow,
            };

            await context.AiConnections.AddAsync(connection, ct);
            await context.SaveChangesAsync(ct); // get generated Id

            // Seed the 3 default model endpoints
            var endpoints = DefaultEndpoints.Select(ep => new AiModelEndpoint
            {
                AiConnectionId = connection.Id,
                EndpointType   = ep.Type,
                Path           = ep.Path,
                HttpMethod     = ep.Method,
                IsEnabled      = true
            }).ToList();

            await context.AiModelEndpoints.AddRangeAsync(endpoints, ct);
            await context.SaveChangesAsync(ct);

            logger.LogInformation("Created AI connection for provider {Provider} (id={Id}) with {Count} default endpoints",
                connection.Provider, connection.Id, endpoints.Count);

            var response = new AiConnectionResponse
            {
                Id                    = connection.Id,
                Provider              = connection.Provider,
                DefaultModel          = connection.DefaultModel,
                SystemPrompt          = connection.SystemPrompt,
                Temperature           = connection.Temperature,
                MaxTokens             = connection.MaxTokens,
                RateLimitRpm          = connection.RateLimitRpm,
                RateLimitTpm          = connection.RateLimitTpm,
                CostPer1kInputTokens  = connection.CostPer1kInputTokens,
                CostPer1kOutputTokens = connection.CostPer1kOutputTokens,
                IsActive              = connection.IsActive,
                CreatedAt             = connection.CreatedAt,
                UpdatedAt             = connection.UpdatedAt,
                ModelEndpoints        = endpoints.Select(e => new AiModelEndpointResponse
                {
                    Id             = e.Id,
                    AiConnectionId = e.AiConnectionId,
                    EndpointType   = e.EndpointType,
                    Path           = e.Path,
                    HttpMethod     = e.HttpMethod,
                    IsEnabled      = e.IsEnabled,
                    Notes          = e.Notes
                }).ToList()
            };

            await SendAsync(new HttpResponse<AiConnectionResponse>(response, "AI connection created.", HttpStatusCode.Created), cancellation: ct);
        }
    }
}
