using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpResponse.Core.Ai.Configs;
using GenReport.Infrastructure.Models.Shared;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Ai.Configs
{
    public class GetAiConfigs(ApplicationDbContext context) 
        : EndpointWithoutRequest<HttpResponse<IEnumerable<AiConfigResponse>>>
    {
        public override void Configure()
        {
            Get("/ai/connections/{connectionId}/configs");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var connectionId = Route<long>("connectionId");

            var configs = await context.AiConfigs
                .Where(c => c.AiConnectionId == connectionId && c.IsActive)
                .Select(c => new AiConfigResponse
                {
                    Id = c.Id,
                    Type = c.Type,
                    Value = c.Value,
                    AiConnectionId = c.AiConnectionId,
                    ModelId = c.ModelId,
                    IsActive = c.IsActive,
                    Version = c.Version,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync(ct);

            await SendAsync(new HttpResponse<IEnumerable<AiConfigResponse>>(configs, "Success", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
