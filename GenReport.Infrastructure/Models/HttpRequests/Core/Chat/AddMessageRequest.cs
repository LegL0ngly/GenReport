namespace GenReport.Infrastructure.Models.HttpRequests.Core.Chat
{
    public class AddMessageRequest
    {
        public required string Role { get; set; }
        public required string Content { get; set; }
        public Microsoft.AspNetCore.Http.IFormFileCollection? Attachments { get; set; }
    }
}
