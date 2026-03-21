namespace GenReport.Infrastructure.Models.HttpRequests.Core.Ai
{
    public class AddAiConnectionRequest
    {
        public required string Provider { get; set; }
        public required string ApiKey { get; set; }
        public required string DefaultModel { get; set; }
        public string? SystemPrompt { get; set; }
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public int? RateLimitRpm { get; set; }
        public int? RateLimitTpm { get; set; }
        public decimal? CostPer1kInputTokens { get; set; }
        public decimal? CostPer1kOutputTokens { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
