using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpResponse.Dashboard;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace GenReport.Endpoints.Dashboard
{
    /// <summary>
    /// GET /reports/recent?limit=20
    /// Returns the most recently generated reports for the authenticated user,
    /// including the originating session details. The fileUrl field is null when
    /// the file has not been uploaded to storage yet.
    /// </summary>
    public class GetRecentReports(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<GetRecentReports> logger)
        : EndpointWithoutRequest<HttpResponse<RecentReportsResult>>
    {
        public override void Configure()
        {
            Get("/reports/recent");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var userId = currentUserService.LoggedInUserId();

            // Read optional ?limit query parameter; default to 20, cap at 100.
            if (!int.TryParse(Query<string>("limit"), out var limit) || limit <= 0)
                limit = 20;
            if (limit > 100)
                limit = 100;

            logger.LogInformation(
                "[RecentReports] Fetching {Limit} recent reports for user {UserId}", limit, userId);

            try
            {
                // Reports are owned by the user via Query.CreatedById.
                // Join through MessageReport → ChatMessage → ChatSession to get session info.
                var reports = await context.Reports
                    .AsNoTracking()
                    .Where(r => r.Query.CreatedById == userId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(limit)
                    .Select(r => new RecentReportDto
                    {
                        Id          = r.Id.ToString(),
                        Name        = r.Name,
                        Query       = r.Query.Rawtext,
                        // Find the session this report belongs to via the message_reports link table.
                        SessionId   = context.MessageReports
                                          .Where(mr => mr.ReportId == r.Id)
                                          .Select(mr => mr.Message.SessionId.ToString())
                                          .FirstOrDefault() ?? "",
                        SessionName = context.MessageReports
                                          .Where(mr => mr.ReportId == r.Id)
                                          .Select(mr => mr.Message.Session.Title ?? "Untitled")
                                          .FirstOrDefault() ?? "Untitled",
                        // Map MIME type → human-readable format string.
                        Format      = r.MediaFile.MimeType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                                          ? "excel"
                                          : r.MediaFile.MimeType == "application/pdf"
                                              ? "pdf"
                                              : "csv",
                        NoOfRows    = r.NoOfRows,
                        FileUrl     = r.MediaFile.StorageUrl,
                        CreatedAt   = r.CreatedAt,
                    })
                    .ToListAsync(ct);

                var result = new RecentReportsResult
                {
                    Reports = reports,
                    Total   = reports.Count,
                };

                await SendAsync(
                    new HttpResponse<RecentReportsResult>(result, "SUCCESS", HttpStatusCode.OK),
                    200, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching recent reports for user {UserId}", userId);
                await SendAsync(
                    new HttpResponse<RecentReportsResult>(
                        HttpStatusCode.InternalServerError,
                        "An unexpected error occurred.",
                        "ERR_INTERNAL",
                        []),
                    500, ct);
            }
        }
    }
}
