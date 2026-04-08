using FastEndpoints;
using GenReport.DB.Domain.Entities.Core;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Databases;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Infrastructure.Security.Encryption;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Databases
{
    public class AddDatabase(
        ApplicationDbContext context,
        ILogger<AddDatabase> logger,
        ICurrentUserService currentUserService,
        ICredentialEncryptorFactory encryptorFactory) : Endpoint<AddDatabaseRequest, HttpResponse<string>>
    {
        public override void Configure()
        {
            Post("/connections");
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

            // Encrypt the password before persisting
            var passwordEncryptor = encryptorFactory.GetEncryptor(CredentialType.Password);
            var encryptedPassword  = string.IsNullOrEmpty(req.Password)
                ? string.Empty
                : passwordEncryptor.Encrypt(req.Password);

            var connectionStringEncryptor = encryptorFactory.GetEncryptor(CredentialType.ConnectionString);
            var encryptedConnectionString = string.IsNullOrEmpty(req.ConnectionString)
                ? string.Empty
                : connectionStringEncryptor.Encrypt(req.ConnectionString);

            var newDatabase = new Database
            {
                Name = req.DatabaseName,
                DatabaseAlias = req.Name,
                Type = req.DatabaseType,
                ConnectionString = encryptedConnectionString,
                ServerAddress = req.HostName ?? string.Empty,
                Port = req.Port,
                Username = req.UserName ?? string.Empty,
                Password = encryptedPassword,
                Description = req.Description ?? string.Empty,
                Status = "Active",
                SizeInBytes = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Provider = req.Provider,
                MaxRowsToReturn = req.MaxRowsToReturn
            };

            await context.Databases.AddAsync(newDatabase, ct);
            await context.SaveChangesAsync(ct);

            await SendAsync(new HttpResponse<string>("Success", "Database connection successfully added.", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
