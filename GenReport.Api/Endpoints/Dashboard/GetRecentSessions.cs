using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpResponse.Dashboard;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GenReport.Endpoints.Dashboard
{
    /// <summary>
    /// GET /dashboard/recent-sessions?limit=10
    /// Returns the most recently active chat sessions with pre-computed message and report counts.
    /// </summary>
    public class GetRecentSessions(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<GetRecentSessions> logger)
        : EndpointWithoutRequest<HttpResponse<List<RecentSessionDto>>>
    {
        public override void Configure()
        {
            Get("dashboard/recent-sessions");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var userId = currentUserService.LoggedInUserId();

            // Read optional ?limit query parameter; default to 10, cap at 100.
            if (!int.TryParse(Query<string>("limit"), out var limit) || limit <= 0)
                limit = 10;
            if (limit > 100)
                limit = 100;

            logger.LogInformation(
                "[RecentSessions] Fetching {Limit} recent sessions for user {UserId}", limit, userId);

            // Subquery counts avoid N+1 — EF translates these as correlated subqueries.
            var sessions = await context.ChatSessions
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .Take(limit)
                .Select(s => new RecentSessionDto
                {
                    Id           = s.Id.ToString(),
                    Title        = s.Title ?? "Untitled",
                    MessageCount = s.Messages.Count(),
                    ReportCount  = context.MessageReports
                                       .Count(mr => mr.Message.SessionId == s.Id),
                    CreatedAt    = s.CreatedAt,
                    UpdatedAt    = s.UpdatedAt,
                })
                .ToListAsync(ct);

            await SendAsync(
                new HttpResponse<List<RecentSessionDto>>(sessions, "SUCCESS"),
                200, ct);
        }
    }
}
