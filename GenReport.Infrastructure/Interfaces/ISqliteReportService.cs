namespace GenReport.Infrastructure.Interfaces
{
    /// <summary>
    /// Reads all tables from a SQLite file, generates Excel and PDF reports,
    /// and emails both attachments to the requesting user.
    /// </summary>
    public interface ISqliteReportService
    {
        /// <summary>
        /// Processes the given SQLite file and sends Excel + PDF reports via email
        /// to the user identified by <paramref name="userId"/>.
        /// </summary>
        /// <param name="fileData">Raw bytes of the .sqlite / .db file.</param>
        /// <param name="fileName">Original file name (used in the email subject).</param>
        /// <param name="userId">The ID of the requesting user — used to look up their email address.</param>
        /// <param name="ct">Cancellation token.</param>
        Task ExportAndEmailAsync(byte[] fileData, string fileName, string userId, CancellationToken ct = default);
    }
}
