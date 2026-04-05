using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpResponse.Core.Databases;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Databases
{
    public class ListDatabases(ApplicationDbContext context, ILogger<ListDatabases> logger, ICurrentUserService currentUserService) : EndpointWithoutRequest<HttpResponse<List<DatabaseResponse>>>
    {
        public override void Configure()
        {
            Get("/connections");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var userId = currentUserService.LoggedInUserId();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                logger.LogError("Invalid user context");
                await SendAsync(new HttpResponse<List<DatabaseResponse>>(HttpStatusCode.Unauthorized, "Unauthorized", "ERR_UNAUTHORIZED", []), cancellation: ct);
                return;
            }

            var databases = await context.Databases
                .Select(d => new DatabaseResponse
                {
                    Id = d.Id.ToString(),
                    Name = d.Name,
                    DatabaseAlias = d.DatabaseAlias,
                    DatabaseType = d.Type,
                    HostName = d.ServerAddress,
                    Port = d.Port,
                    UserName = "••••••••",
                    DatabaseName = d.Name,
                    Description = d.Description,
                    Status = d.Status,
                    SizeInBytes = d.SizeInBytes,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                })
                .ToListAsync(ct);

            await SendAsync(new HttpResponse<List<DatabaseResponse>>(databases, "Databases retrieved successfully.", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
