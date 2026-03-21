using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Ai;
using GenReport.Infrastructure.Models.Shared;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Ai
{
    /// <summary>PUT /ai/connections/{id}/endpoints/{endpointId} — update path, method, enabled state, or notes.</summary>
    public class EditAiModelEndpoint(ApplicationDbContext context, ILogger<EditAiModelEndpoint> logger)
        : Endpoint<EditAiModelEndpointRequest, HttpResponse<string>>
    {
        public override void Configure()
        {
            Put("/ai/connections/{id}/endpoints/{endpointId}");
        }

        public override async Task HandleAsync(EditAiModelEndpointRequest req, CancellationToken ct)
        {
            var connectionId = Route<long>("id");
            var endpointId   = Route<long>("endpointId");

            var endpoint = await context.AiModelEndpoints
                .FirstOrDefaultAsync(e => e.Id == endpointId && e.AiConnectionId == connectionId, ct);

            if (endpoint == null)
            {
                await SendAsync(new HttpResponse<string>(HttpStatusCode.NotFound, "Model endpoint not found.", "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            if (!string.IsNullOrEmpty(req.Path))       endpoint.Path       = req.Path;
            if (!string.IsNullOrEmpty(req.HttpMethod))  endpoint.HttpMethod  = req.HttpMethod;
            if (req.IsEnabled.HasValue)                 endpoint.IsEnabled  = req.IsEnabled.Value;
            if (req.Notes != null)                      endpoint.Notes      = req.Notes;

            context.AiModelEndpoints.Update(endpoint);
            await context.SaveChangesAsync(ct);

            await SendAsync(new HttpResponse<string>("Success", "Model endpoint updated.", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
