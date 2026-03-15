using GenReport.DB.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace GenReport.Infrastructure.Models.HttpRequests.Core.Databases
{
    public class EditDatabaseRequest
    {
        [Required]
        public long Id { get; set; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public DbProvider? Provider { get; set; }

        public string? ConnectionString { get; set; }

        public string? ServerAddress { get; set; }

        public int? Port { get; set; }

        public string? Username { get; set; }

        public string? Password { get; set; }

        public string? Description { get; set; }
    }
}
