using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Databases;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Databases
{
    public class EditDatabase(ApplicationDbContext context, ILogger<EditDatabase> logger, ICurrentUserService currentUserService) : Endpoint<EditDatabaseRequest, HttpResponse<string>>
    {
        public override void Configure()
        {
            Put("/databases");
        }

        public override async Task HandleAsync(EditDatabaseRequest req, CancellationToken ct)
        {
            var userId = currentUserService.LoggedInUserId();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                logger.LogError("Invalid user context");
                await SendAsync(new HttpResponse<string>(HttpStatusCode.Unauthorized, "Unauthorized", "ERR_UNAUTHORIZED", []), cancellation: ct);
                return;
            }

            var existingDb = await context.Databases.FirstOrDefaultAsync(d => d.Id == req.Id, ct);

            if (existingDb == null)
            {
                await SendAsync(new HttpResponse<string>(HttpStatusCode.NotFound, "Database connection not found.", "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            // Update properties if provided
            if (!string.IsNullOrEmpty(req.Name)) existingDb.Name = req.Name;
            if (!string.IsNullOrEmpty(req.Type)) existingDb.Type = req.Type;
            if (!string.IsNullOrEmpty(req.ConnectionString)) existingDb.ConnectionString = req.ConnectionString;
            if (req.ServerAddress != null) existingDb.ServerAddress = req.ServerAddress;
            if (req.Port.HasValue) existingDb.Port = req.Port.Value;
            if (req.Username != null) existingDb.Username = req.Username;
            if (req.Password != null) existingDb.Password = req.Password; 
            if (req.Provider.HasValue) existingDb.Provider = req.Provider.Value;
            if (req.Description != null) existingDb.Description = req.Description;

            existingDb.UpdatedAt = DateTime.UtcNow;

            context.Databases.Update(existingDb);
            await context.SaveChangesAsync(ct);

            await SendAsync(new HttpResponse<string>("Success", "Database connection successfully updated.", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
