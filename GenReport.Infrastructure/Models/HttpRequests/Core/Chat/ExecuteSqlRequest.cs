using System.ComponentModel.DataAnnotations;

namespace GenReport.Infrastructure.Models.HttpRequests.Core.Chat
{
    public class ExecuteSqlRequest
    {
        [Required]
        public required string DatabaseConnectionId { get; set; }

        [Required]
        public required string Query { get; set; }
    }
}
