using FastEndpoints;
using FluentValidation;
using GenReport.Infrastructure.Models.HttpRequests.Core.Ai.Configs;

namespace GenReport.Api.Validations.Core.Ai.Configs
{
    public class AddAiConfigRequestValidator : Validator<AddAiConfigRequest>
    {
        public AddAiConfigRequestValidator()
        {
            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Invalid AiConfigType.");

            RuleFor(x => x.Value)
                .NotEmpty().WithMessage("Value cannot be empty.");
        }
    }
}
