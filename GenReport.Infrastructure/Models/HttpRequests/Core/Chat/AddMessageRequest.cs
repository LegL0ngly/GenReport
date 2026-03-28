namespace GenReport.Infrastructure.Models.HttpRequests.Core.Chat
{
    public class VercelAiMessagePart
    {
        public string Type { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class VercelAiMessage
    {
        public string Id { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        // Sometimes Next.js AI SDK sends 'content' directly instead of 'parts' depending on version or configuration
        public string? Content { get; set; } 
        public List<VercelAiMessagePart>? Parts { get; set; }
    }

    public class AddMessageRequest
    {
        public string ModelId { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;

        // As per global guidelines, string datatype for Id, parsed in backend.
        public string? SessionId { get; set; }

        public List<VercelAiMessage> Messages { get; set; } = new();
        public string Trigger { get; set; } = string.Empty;

        public Microsoft.AspNetCore.Http.IFormFileCollection? Attachments { get; set; }
    }
}
