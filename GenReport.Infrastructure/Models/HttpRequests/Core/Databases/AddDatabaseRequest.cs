using GenReport.DB.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace GenReport.Infrastructure.Models.HttpRequests.Core.Databases
{
    public class AddDatabaseRequest
    {
        [Required]
        public required string Name { get; set; }

        [Required]
        public required string Type { get; set; }

        [Required]
        public required DbProvider Provider { get; set; }

        [Required]
        public required string ConnectionString { get; set; }

        public required string ServerAddress { get; set; }

        public int Port { get; set; }

        public required string Username { get; set; }

        public required string Password { get; set; }

        public required string Description { get; set; }
    }
}
