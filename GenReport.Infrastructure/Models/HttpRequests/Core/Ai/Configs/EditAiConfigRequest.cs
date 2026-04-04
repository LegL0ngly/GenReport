namespace GenReport.Infrastructure.Models.HttpRequests.Core.Ai.Configs
{
    public class EditAiConfigRequest
    {
        public string? Value { get; set; }
        public bool? IsActive { get; set; }
        public string? ModelId { get; set; }
    }
}
