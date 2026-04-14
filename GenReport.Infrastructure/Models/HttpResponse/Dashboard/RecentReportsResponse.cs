namespace GenReport.Infrastructure.Models.HttpResponse.Dashboard
{
    /// <summary>
    /// Wrapper returned by GET /reports/recent.
    /// </summary>
    public class RecentReportsResult
    {
        public required IList<RecentReportDto> Reports { get; set; }
        public int Total { get; set; }
    }

    /// <summary>
    /// A single report row in the recent-reports list.
    /// </summary>
    public class RecentReportDto
    {
        public required string Id          { get; set; }
        public required string Name        { get; set; }
        public required string Query       { get; set; }
        public required string SessionId   { get; set; }
        public required string SessionName { get; set; }
        /// <summary>excel | pdf | csv</summary>
        public required string Format      { get; set; }
        public int             NoOfRows    { get; set; }
        /// <summary>Null when the file has not been uploaded/generated yet.</summary>
        public string?         FileUrl     { get; set; }
        public DateTime        CreatedAt   { get; set; }
    }
}
