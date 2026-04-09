using Microsoft.AspNetCore.Http;

namespace GenReport.Infrastructure.Models.HttpRequests.Core.Reports
{
    public sealed class ExportSqliteReportRequest
    {
        /// <summary>The .sqlite / .db file to export.</summary>
        public IFormFile File { get; set; } = null!;
    }
}
