using FastEndpoints;
using GenReport.DB.Domain.Entities.Core;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Databases;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Databases
{
    public class AddDatabase(ApplicationDbContext context, ILogger<AddDatabase> logger, ICurrentUserService currentUserService) : Endpoint<AddDatabaseRequest, HttpResponse<string>>
    {
        public override void Configure()
        {
            Post("/databases");
        }

        public override async Task HandleAsync(AddDatabaseRequest req, CancellationToken ct)
        {
            var userId = currentUserService.LoggedInUserId();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                logger.LogError("Invalid user context");
                await SendAsync(new HttpResponse<string>(HttpStatusCode.Unauthorized, "Unauthorized", "ERR_UNAUTHORIZED", []), cancellation: ct);
                return;
            }

            var newDatabase = new Database
            {
                Name = req.Name,
                Type = req.Type,
                ConnectionString = req.ConnectionString,
                ServerAddress = req.ServerAddress ?? string.Empty,
                Port = req.Port,
                Username = req.Username ?? string.Empty,
                Password = req.Password ?? string.Empty,
                Description = req.Description ?? string.Empty,
                Status = "Active",
                SizeInBytes = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Provider = req.Provider
            };

            await context.Databases.AddAsync(newDatabase, ct);
            await context.SaveChangesAsync(ct);

            await SendAsync(new HttpResponse<string>("Success", "Database connection successfully added.", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
