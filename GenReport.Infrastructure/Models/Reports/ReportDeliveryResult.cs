namespace GenReport.Infrastructure.Models.Reports
{
    /// <summary>
    /// Result returned by <see cref="GenReport.Infrastructure.Interfaces.ISqliteReportService.ExportAndDeliverAsync"/>.
    /// </summary>
    public sealed record ReportDeliveryResult(
        /// <summary>
        /// Public R2 URL of the uploaded Excel file, or <c>null</c> when R2 is not
        /// configured (or the upload failed) and the file was attached to the email instead.
        /// </summary>
        string? R2Url,

        /// <summary>Number of data rows in the SQLite results table.</summary>
        int NoOfRows,

        /// <summary>Number of columns returned by the query.</summary>
        int NoOfColumns,

        /// <summary>Size of the generated Excel file in bytes.</summary>
        long ExcelSizeBytes
    );
}
