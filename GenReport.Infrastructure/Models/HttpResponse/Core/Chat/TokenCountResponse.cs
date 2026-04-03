namespace GenReport.Infrastructure.Models.HttpResponse.Core.Chat
{
    public class TokenCountResponse
    {
        public bool IsSuccess { get; set; }
        public int TotalTokens { get; set; }
        public int MaxTokens { get; set; }
        public bool IsExceeded { get; set; }
        public string CalculationMethod { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }
}
