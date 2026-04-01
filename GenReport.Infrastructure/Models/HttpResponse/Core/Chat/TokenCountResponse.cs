namespace GenReport.Infrastructure.Models.HttpResponse.Core.Chat
{
    public class TokenCountResponse
    {
        public bool IsSuccess { get; set; } = true;
        public string? ErrorMessage { get; set; }
        
        /// <summary>Total tokens calculated across all messages in the session.</summary>
        public int TotalTokens { get; set; }
        
        /// <summary>The maximum tokens allowed / configured for this connection.</summary>
        public int? MaxTokens { get; set; }
        
        /// <summary>Whether the total tokens exceeded the allowed limit or max context length.</summary>
        public bool IsExceeded { get; set; }
        
        /// <summary>Details on how tokens were calculated (e.g. Provider API, Fallback Tiktoken, etc.)</summary>
        public string? CalculationMethod { get; set; }
    }
}
