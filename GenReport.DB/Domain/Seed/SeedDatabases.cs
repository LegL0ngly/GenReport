using GenReport.DB.Domain.Entities.Core;
using GenReport.DB.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenReport.DB.Domain.Seed
{
    public partial class ApplicationDBContextSeeder
    {
        public async Task SeedDatabases()
        {
            var providers  = new Dictionary<string, string>();

            var database = new Database
            {
                BackupSchedule = (int)TimeSpan.FromDays(1).TotalMinutes,
                ConnectionString = "Server=localhost;Database=db;Username=postgres;Password=postgres;",
                Description = "Support database to run test queries on",
                Name = "testDB",
                Password = "postgres",
                ServerAddress = "127.0.0.1",
                SizeInBytes = 0,
                Status = "InActive",
                Type = DatabaseType.PostgreSQL.ToString(),
                Provider = DbProvider.NpgSql,
                Username = "postgres",
                Port = 5433,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

           await  applicationDbContext.Databases.AddAsync(database);
           await applicationDbContext.SaveChangesAsync();

        }
    }
}
