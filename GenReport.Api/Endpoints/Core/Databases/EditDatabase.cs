using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Databases;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Infrastructure.Security.Encryption;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Databases
{
    public class EditDatabase(
        ApplicationDbContext context,
        ILogger<EditDatabase> logger,
        ICurrentUserService currentUserService,
        ICredentialEncryptorFactory encryptorFactory) : Endpoint<EditDatabaseRequest, HttpResponse<string>>
    {
        public override void Configure()
        {
            Put("/connections");
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
            if (!string.IsNullOrEmpty(req.DatabaseAlias)) existingDb.DatabaseAlias = req.DatabaseAlias;
            if (!string.IsNullOrEmpty(req.DatabaseType)) existingDb.Type = req.DatabaseType;
            if (!string.IsNullOrEmpty(req.ConnectionString))
                existingDb.ConnectionString = encryptorFactory.GetEncryptor(CredentialType.ConnectionString).Encrypt(req.ConnectionString);
            if (req.HostName != null) existingDb.ServerAddress = req.HostName;
            if (req.Port.HasValue) existingDb.Port = req.Port.Value;
            if (req.UserName != null) existingDb.Username = req.UserName;
            if (req.Password != null)
            {
                // Encrypt the updated password before persisting
                existingDb.Password = encryptorFactory.GetEncryptor(CredentialType.Password).Encrypt(req.Password);
            }
            if (req.Provider.HasValue) existingDb.Provider = req.Provider.Value;
            if (req.Description != null) existingDb.Description = req.Description;
            if (req.MaxRowsToReturn.HasValue) existingDb.MaxRowsToReturn = req.MaxRowsToReturn.Value;

            existingDb.UpdatedAt = DateTime.UtcNow;

            context.Databases.Update(existingDb);
            await context.SaveChangesAsync(ct);

            await SendAsync(new HttpResponse<string>("Success", "Database connection successfully updated.", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
