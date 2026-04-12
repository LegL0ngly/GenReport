using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpResponse.Dashboard;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Infrastructure.Static.Constants;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GenReport.Endpoints.Dashboard
{
    /// <summary>
    /// GET /dashboard/stats
    /// Returns aggregate counts of reports, queries, and chat sessions for the authenticated user.
    /// </summary>
    public class GetDashboardStats(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<GetDashboardStats> logger)
        : EndpointWithoutRequest<HttpResponse<DashboardStatsDto>>
    {
        public override void Configure()
        {
            Get("dashboard/stats");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var userId = currentUserService.LoggedInUserId();

            logger.LogInformation("[DashboardStats] Fetching stats for user {UserId}", userId);

            // All three counts are simple scalar aggregates — no joins.
            var totalReports = await context.Reports
                .Where(r => r.Query.CreatedById == userId)
                .CountAsync(ct);

            var totalQueries = await context.Queries
                .Where(q => q.CreatedById == userId)
                .CountAsync(ct);

            var totalChats = await context.ChatSessions
                .Where(s => s.UserId == userId)
                .CountAsync(ct);

            var stats = new DashboardStatsDto
            {
                TotalReports = totalReports,
                TotalQueries = totalQueries,
                TotalChats   = totalChats,
            };

            await SendAsync(new HttpResponse<DashboardStatsDto>(stats, "SUCCESS"), 200, ct);
        }
    }
}
