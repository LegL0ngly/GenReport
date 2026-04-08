using System.ComponentModel.DataAnnotations;

namespace GenReport.Infrastructure.Models.HttpRequests.Core.Chat
{
    public class ValidateSqlRequest
    {
        [Required]
        public required string DatabaseConnectionId { get; set; }

        [Required]
        public required string Query { get; set; }
    }
}
