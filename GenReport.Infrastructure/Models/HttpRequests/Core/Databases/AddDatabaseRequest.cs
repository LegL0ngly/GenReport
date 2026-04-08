using GenReport.DB.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace GenReport.Infrastructure.Models.HttpRequests.Core.Databases
{
    public class AddDatabaseRequest
    {
        [Required]
        public required string Name { get; set; }

        [Required]
        public required string DatabaseType { get; set; }

        [Required]
        public required DbProvider Provider { get; set; }

        [Required]
        public required string HostName { get; set; }

        public int Port { get; set; }

        public required string UserName { get; set; }

        public required string DatabaseName { get; set; }
        
        public string? Password { get; set; }
        
        public string? ConnectionString { get; set; }

        public string? Description { get; set; }
        
        [Range(1, 10000)]
        public int MaxRowsToReturn { get; set; } = 20;
    }
}
