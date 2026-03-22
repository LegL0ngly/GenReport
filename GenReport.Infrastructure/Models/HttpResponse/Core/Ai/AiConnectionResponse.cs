namespace GenReport.Infrastructure.Models.HttpResponse.Core.Ai
{
    /// <summary>
    /// Public projection of AiConnection. The ApiKey is intentionally excluded.
    /// </summary>
    public class AiConnectionResponse
    {
        public long Id { get; set; }
        public required string Provider { get; set; }
        public required string DefaultModel { get; set; }
        public string? SystemPrompt { get; set; }
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public int? RateLimitRpm { get; set; }
        public int? RateLimitTpm { get; set; }
        public decimal? CostPer1kInputTokens { get; set; }
        public decimal? CostPer1kOutputTokens { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
