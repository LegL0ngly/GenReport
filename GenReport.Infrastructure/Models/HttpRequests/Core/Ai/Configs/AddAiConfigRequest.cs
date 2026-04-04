using GenReport.DB.Domain.Entities.Core;

namespace GenReport.Infrastructure.Models.HttpRequests.Core.Ai.Configs
{
    public class AddAiConfigRequest
    {
        public AiConfigType Type { get; set; }
        public string Value { get; set; } = string.Empty;
        public string? ModelId { get; set; }
    }
}
