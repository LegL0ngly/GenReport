using GenReport.DB.Domain.Entities.Core;

namespace GenReport.Infrastructure.Models.HttpResponse.Core.Ai.Configs
{
    public class AiConfigResponse
    {
        public long Id { get; set; }
        public AiConfigType Type { get; set; }
        public string Value { get; set; } = string.Empty;
        public long AiConnectionId { get; set; }
        public string? ModelId { get; set; }
        public bool IsActive { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
