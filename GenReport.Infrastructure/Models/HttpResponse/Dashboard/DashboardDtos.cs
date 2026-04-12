namespace GenReport.Infrastructure.Models.HttpResponse.Dashboard
{
    /// <summary>Aggregate totals for the authenticated user's dashboard.</summary>
    public sealed class DashboardStatsDto
    {
        public int TotalReports { get; set; }
        public int TotalQueries { get; set; }
        public int TotalChats   { get; set; }
    }

    /// <summary>A single chat session summary for the recent-sessions list.</summary>
    public sealed class RecentSessionDto
    {
        public string    Id           { get; set; } = "";
        public string    Title        { get; set; } = "";
        public int       MessageCount { get; set; }
        public int       ReportCount  { get; set; }
        public DateTime  CreatedAt    { get; set; }
        public DateTime  UpdatedAt    { get; set; }
    }
}
