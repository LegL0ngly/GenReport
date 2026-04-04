using FastEndpoints;
using GenReport.DB.Domain.Entities.Core;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Ai.Configs;
using GenReport.Infrastructure.Models.HttpResponse.Core.Ai.Configs;
using GenReport.Infrastructure.Models.Shared;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Ai.Configs
{
    public class AddAiConfig(ApplicationDbContext context) 
        : Endpoint<AddAiConfigRequest, HttpResponse<AiConfigResponse>>
    {
        public override void Configure()
        {
            Post("/ai/connections/{connectionId}/configs");
        }

        public override async Task HandleAsync(AddAiConfigRequest req, CancellationToken ct)
        {
            var connectionId = Route<long>("connectionId");
            var connection = await context.AiConnections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);

            if (connection == null)
            {
                await SendAsync(new HttpResponse<AiConfigResponse>(HttpStatusCode.NotFound, "AI connection not found.", "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            var existingConfigs = await context.AiConfigs
                .Where(c => c.AiConnectionId == connectionId && c.Type == req.Type && c.ModelId == req.ModelId)
                .ToListAsync(ct);

            int nextVersion = 1;

            if (existingConfigs.Any())
            {
                foreach (var existing in existingConfigs.Where(c => c.IsActive))
                {
                    existing.IsActive = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                nextVersion = existingConfigs.Max(c => c.Version) + 1;
            }

            var newConfig = new AiConfig
            {
                Type = req.Type,
                Value = req.Value,
                AiConnectionId = connectionId,
                ModelId = req.ModelId,
                IsActive = true,
                Version = nextVersion,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await context.AiConfigs.AddAsync(newConfig, ct);
            await context.SaveChangesAsync(ct);

            var response = new AiConfigResponse
            {
                Id = newConfig.Id,
                Type = newConfig.Type,
                Value = newConfig.Value,
                AiConnectionId = newConfig.AiConnectionId,
                ModelId = newConfig.ModelId,
                IsActive = newConfig.IsActive,
                Version = newConfig.Version,
                CreatedAt = newConfig.CreatedAt,
                UpdatedAt = newConfig.UpdatedAt
            };

            await SendAsync(new HttpResponse<AiConfigResponse>(response, "AI config added successfully.", HttpStatusCode.Created), cancellation: ct);
        }
    }
}
