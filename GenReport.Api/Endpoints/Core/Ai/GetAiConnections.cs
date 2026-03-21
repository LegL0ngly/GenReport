using FastEndpoints;
using GenReport.DB.Domain.Enums;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpResponse.Core.Ai;
using GenReport.Infrastructure.Models.Shared;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Ai
{
    /// <summary>GET /ai/connections — returns all AI connections (API key excluded).</summary>
    public class GetAiConnections(ApplicationDbContext context, ILogger<GetAiConnections> logger)
        : EndpointWithoutRequest<HttpResponse<List<AiConnectionResponse>>>
    {
        public override void Configure()
        {
            Get("/ai/connections");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var connections = await context.AiConnections
                .Include(c => c.ModelEndpoints)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new AiConnectionResponse
                {
                    Id                    = c.Id,
                    Provider              = c.Provider,
                    DefaultModel          = c.DefaultModel,
                    SystemPrompt          = c.SystemPrompt,
                    Temperature           = c.Temperature,
                    MaxTokens             = c.MaxTokens,
                    RateLimitRpm          = c.RateLimitRpm,
                    RateLimitTpm          = c.RateLimitTpm,
                    CostPer1kInputTokens  = c.CostPer1kInputTokens,
                    CostPer1kOutputTokens = c.CostPer1kOutputTokens,
                    IsActive              = c.IsActive,
                    CreatedAt             = c.CreatedAt,
                    UpdatedAt             = c.UpdatedAt,
                    ModelEndpoints        = c.ModelEndpoints.Select(e => new AiModelEndpointResponse
                    {
                        Id               = e.Id,
                        AiConnectionId   = e.AiConnectionId,
                        EndpointType     = e.EndpointType,
                        Path             = e.Path,
                        HttpMethod       = e.HttpMethod,
                        IsEnabled        = e.IsEnabled,
                        Notes            = e.Notes
                    }).ToList()
                })
                .ToListAsync(ct);

            await SendAsync(new HttpResponse<List<AiConnectionResponse>>(connections, "OK", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
