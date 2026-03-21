namespace GenReport.Infrastructure.Models.HttpRequests.Core.Ai
{
    public class EditAiModelEndpointRequest
    {
        public string? Path { get; set; }
        public string? HttpMethod { get; set; }
        public bool? IsEnabled { get; set; }
        public string? Notes { get; set; }
    }
}
