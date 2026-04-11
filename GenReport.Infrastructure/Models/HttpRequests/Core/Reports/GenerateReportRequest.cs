using System.ComponentModel.DataAnnotations;

namespace GenReport.Infrastructure.Models.HttpRequests.Core.Reports
{
    public class GenerateReportRequest
    {
        [Required]
        public required string Query { get; set; }

        [Required]
        public required string DatabaseConnectionId { get; set; }

        [Required]
        public required string SessionId { get; set; }

        /// <summary>
        /// Output format: e.g. "excel", "pdf", "csv"
        /// </summary>
        [Required]
        [AllowedValues("excel", "pdf", "csv", ErrorMessage = "Format must be one of: excel, pdf, csv")]
        public required string Format { get; set; }
    }
}
