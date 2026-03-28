using GenReport.DB.Domain.Entities.Core;
using GenReport.DB.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace GenReport.DB.Domain.Seed
{
    public partial class ApplicationDBContextSeeder
    {
        public async Task SeedDatabases()
        {
            if (!await applicationDbContext.Databases.AnyAsync(d => d.DatabaseAlias == "krisDB"))
            {
                var connString = "Server=127.0.0.1;Port=5432;Database=genreport;User Id=postgres;Password=postgres;";
                
                var database = new Database
                {
                    Name = "genreport",
                    DatabaseAlias = "krisDB",
                    Provider = DbProvider.NpgSql,
                    Type = DatabaseType.PostgreSQL.ToString(),
                    ConnectionString = ConnectionStringEncryptor != null ? ConnectionStringEncryptor(connString) : connString,
                    Password = PasswordEncryptor != null ? PasswordEncryptor("postgres") : "postgres",
                    ServerAddress = "127.0.0.1",
                    Port = 5432,
                    Username = "postgres",
                    Status = "Active",
                    Description = "Seeded krisDB",
                    SizeInBytes = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                await applicationDbContext.Databases.AddAsync(database);
                await applicationDbContext.SaveChangesAsync();
                logger.Information("Seeded krisDB Database.");
            }
        }

        public async Task SeedAiConnections()
        {
            if (!await applicationDbContext.AiConnections.AnyAsync(c => c.Provider == "gemini"))
            {
                var geminiConnection = new AiConnection
                {
                    Provider = "gemini",
                    ApiKey = ApiKeyEncryptor != null ? ApiKeyEncryptor("AIzaSyBPxMR7-ySWVXaJlw1BP3pFHSMopNFd6pY") : "AIzaSyBPxMR7-ySWVXaJlw1BP3pFHSMopNFd6pY",
                    DefaultModel = "gemini-1.5-pro",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await applicationDbContext.AiConnections.AddAsync(geminiConnection);
                await applicationDbContext.SaveChangesAsync();
                logger.Information("Seeded Gemini AiConnection.");
            }
        }
    }
}
