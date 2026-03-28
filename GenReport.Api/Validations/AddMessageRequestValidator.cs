using FastEndpoints;
using FluentValidation;
using GenReport.Infrastructure.Models.HttpRequests.Core.Chat;
using Microsoft.AspNetCore.Http;

namespace GenReport.Api.Validations
{
    public class AddMessageRequestValidator : Validator<AddMessageRequest>
    {
        public AddMessageRequestValidator()
        {
            RuleFor(x => x.Messages)
                .NotEmpty()
                .WithMessage("Messages list cannot be empty.");

            RuleFor(x => x.Attachments)
                .Must(attachments => attachments == null || attachments.Count <= 2)
                .WithMessage("You can only upload a maximum of 2 files.")
                .When(x => x.Attachments != null);

            var allowedMimeTypes = new[] { 
                "image/jpeg", "image/png", "image/gif", "image/webp",
                "application/pdf", "text/plain", "application/msword", 
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            };

            RuleForEach(x => x.Attachments)
                .Must(file => file.Length <= 1 * 1024 * 1024)
                .WithMessage((req, file) => $"File {file.FileName} exceeds the maximum size of 1 MB.")
                .Must(file => allowedMimeTypes.Contains(file.ContentType.ToLower()))
                .WithMessage((req, file) => $"File type {file.ContentType} for {file.FileName} is not allowed.")
                .When(x => x.Attachments != null);
        }
    }
}
