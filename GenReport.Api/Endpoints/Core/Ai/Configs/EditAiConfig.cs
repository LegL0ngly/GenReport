using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Ai.Configs;
using GenReport.Infrastructure.Models.Shared;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Ai.Configs
{
    public class EditAiConfig(ApplicationDbContext context) 
        : Endpoint<EditAiConfigRequest, HttpResponse<string>>
    {
        public override void Configure()
        {
            Put("/ai/connections/{connectionId}/configs/{id}");
        }

        public override async Task HandleAsync(EditAiConfigRequest req, CancellationToken ct)
        {
            var connectionId = Route<long>("connectionId");
            var id = Route<long>("id");

            var config = await context.AiConfigs.FirstOrDefaultAsync(c => c.Id == id && c.AiConnectionId == connectionId, ct);

            if (config == null)
            {
                await SendAsync(new HttpResponse<string>(HttpStatusCode.NotFound, "AI config not found.", "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            if (req.Value != null) config.Value = req.Value;
            if (req.IsActive.HasValue) config.IsActive = req.IsActive.Value;
            if (req.ModelId != null) config.ModelId = req.ModelId;

            config.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(ct);

            await SendAsync(new HttpResponse<string>("Success", "AI config updated.", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
