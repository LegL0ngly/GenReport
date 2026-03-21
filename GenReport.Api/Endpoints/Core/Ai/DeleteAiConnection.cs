using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.Shared;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Ai
{
    /// <summary>DELETE /ai/connections/{id} — deletes a connection and cascades to model endpoints.</summary>
    public class DeleteAiConnection(ApplicationDbContext context, ILogger<DeleteAiConnection> logger)
        : EndpointWithoutRequest<HttpResponse<string>>
    {
        public override void Configure()
        {
            Delete("/ai/connections/{id}");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var id = Route<long>("id");
            var connection = await context.AiConnections.FirstOrDefaultAsync(c => c.Id == id, ct);

            if (connection == null)
            {
                await SendAsync(new HttpResponse<string>(HttpStatusCode.NotFound, "AI connection not found.", "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            context.AiConnections.Remove(connection);
            await context.SaveChangesAsync(ct);

            logger.LogInformation("Deleted AI connection {Id} ({Provider})", id, connection.Provider);

            await SendAsync(new HttpResponse<string>("Success", "AI connection and its endpoints deleted.", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
