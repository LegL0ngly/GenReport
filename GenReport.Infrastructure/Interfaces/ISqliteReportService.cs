using GenReport.Infrastructure.Configuration;
using GenReport.Infrastructure.Models.Reports;

namespace GenReport.Infrastructure.Interfaces
{
    /// <summary>
    /// Reads all tables from a SQLite file, generates Excel and PDF reports,
    /// and delivers them to the requesting user — either via R2 link or email attachment.
    /// </summary>
    public interface ISqliteReportService
    {
        /// <summary>
        /// Processes the given SQLite file and sends Excel + PDF reports via email
        /// to the user identified by <paramref name="userId"/>.
        /// Always attaches files directly (used by the manual /reports/sqlite/export endpoint).
        /// </summary>
        /// <param name="fileData">Raw bytes of the .sqlite / .db file.</param>
        /// <param name="fileName">Original file name (used in the email subject).</param>
        /// <param name="userId">The ID of the requesting user — used to look up their email address.</param>
        /// <param name="ct">Cancellation token.</param>
        Task ExportAndEmailAsync(byte[] fileData, string fileName, string userId, CancellationToken ct = default);

        /// <summary>
        /// Processes the given SQLite file, generates Excel + PDF, and delivers them to the user.
        /// When <paramref name="r2Config"/> is configured the Excel is uploaded to R2 and the user
        /// receives an email with a download link; otherwise both files are attached to the email.
        /// </summary>
        /// <param name="fileData">Raw bytes of the .sqlite / .db file.</param>
        /// <param name="fileName">Original file name (used for the report name / email subject).</param>
        /// <param name="userId">The ID of the requesting user.</param>
        /// <param name="r2Config">Optional R2 configuration; pass <c>null</c> or unconfigured to force attachment mode.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// A <see cref="ReportDeliveryResult"/> containing the R2 URL (or <c>null</c>),
        /// row/column counts, and the Excel file size — all needed to persist the Report DB record.
        /// </returns>
        Task<ReportDeliveryResult> ExportAndDeliverAsync(
            byte[] fileData,
            string fileName,
            string userId,
            R2Configuration? r2Config = null,
            CancellationToken ct = default);
    }
}

