using GenReport.DB.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace GenReport.Infrastructure.Models.HttpRequests.Core.Databases
{
    public class EditDatabaseRequest
    {
        [Required]
        public long Id { get; set; }

        public string? Name { get; set; }

        public string? DatabaseAlias { get; set; }

        public string? DatabaseType { get; set; }

        public DbProvider? Provider { get; set; }

        public string? HostName { get; set; }

        public int? Port { get; set; }

        public string? UserName { get; set; }

        public string? DatabaseName { get; set; }
        
        public string? Password { get; set; }

        public string? ConnectionString { get; set; }

        public string? Description { get; set; }
        
        public int? MaxRowsToReturn { get; set; }
    }
}
