using GenReport.DB.Domain.Enums;

namespace GenReport.Infrastructure.Models.HttpResponse.Core.Ai
{
    public class AiModelEndpointResponse
    {
        public long Id { get; set; }
        public long AiConnectionId { get; set; }
        public AiEndpointType EndpointType { get; set; }
        public required string Path { get; set; }
        public required string HttpMethod { get; set; }
        public bool IsEnabled { get; set; }
        public string? Notes { get; set; }
    }
}
