using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpResponse.Core.Ai;
using GenReport.Infrastructure.Models.Shared;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Ai
{
    /// <summary>GET /ai/connections/{id}/endpoints — returns all model endpoints for a connection.</summary>
    public class GetAiModelEndpoints(ApplicationDbContext context)
        : EndpointWithoutRequest<HttpResponse<List<AiModelEndpointResponse>>>
    {
        public override void Configure()
        {
            Get("/ai/connections/{id}/endpoints");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var id = Route<long>("id");

            var exists = await context.AiConnections.AnyAsync(c => c.Id == id, ct);
            if (!exists)
            {
                await SendAsync(new HttpResponse<List<AiModelEndpointResponse>>(
                    HttpStatusCode.NotFound, "AI connection not found.", "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            var endpoints = await context.AiModelEndpoints
                .Where(e => e.AiConnectionId == id)
                .OrderBy(e => e.EndpointType)
                .Select(e => new AiModelEndpointResponse
                {
                    Id             = e.Id,
                    AiConnectionId = e.AiConnectionId,
                    EndpointType   = e.EndpointType,
                    Path           = e.Path,
                    HttpMethod     = e.HttpMethod,
                    IsEnabled      = e.IsEnabled,
                    Notes          = e.Notes
                })
                .ToListAsync(ct);

            await SendAsync(new HttpResponse<List<AiModelEndpointResponse>>(endpoints, "OK", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
