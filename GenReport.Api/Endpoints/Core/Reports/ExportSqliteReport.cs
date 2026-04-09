using FastEndpoints;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.HttpRequests.Core.Reports;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Reports
{
    /// <summary>
    /// Accepts a SQLite file upload, generates Excel and PDF reports from all its tables,
    /// and emails both to the currently authenticated user.
    /// </summary>
    public class ExportSqliteReport(
        ICurrentUserService currentUserService,
        ISqliteReportService sqliteReportService) : EndpointWithoutRequest
    {
        public override void Configure()
        {
            Post("/reports/sqlite/export");
            AllowFileUploads();
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            if (!Form.Files.Any())
            {
                await SendAsync(
                    new HttpResponse<object>(HttpStatusCode.BadRequest, "No file uploaded.", "ERR_NO_FILE", []),
                    cancellation: ct);
                return;
            }

            var file = Form.Files[0];

            if (file.Length == 0)
            {
                await SendAsync(
                    new HttpResponse<object>(HttpStatusCode.BadRequest, "Uploaded file is empty.", "ERR_EMPTY_FILE", []),
                    cancellation: ct);
                return;
            }

            byte[] fileData;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                fileData = ms.ToArray();
            }

            var userId = currentUserService.LoggedInUserId().ToString();

            Logger.LogInformation("SQLite export requested by user {UserId}, file: {FileName} ({Bytes} bytes)",
                userId, file.FileName, fileData.Length);

            // Fire-and-forget on a background thread so the HTTP response returns immediately
            _ = Task.Run(async () =>
            {
                try
                {
                    await sqliteReportService.ExportAndEmailAsync(fileData, file.FileName, userId);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "SQLite report export failed for user {UserId}, file {FileName}",
                        userId, file.FileName);
                }
            }, CancellationToken.None);

            await SendAsync(
                new HttpResponse<object>(HttpStatusCode.Accepted, "Report is being generated. You'll receive an email shortly.", null, []),
                (int)HttpStatusCode.Accepted,
                cancellation: ct);
        }
    }
}
