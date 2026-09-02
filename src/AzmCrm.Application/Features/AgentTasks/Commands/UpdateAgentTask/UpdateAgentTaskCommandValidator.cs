using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.AgentTasks.Commands.UpdateAgentTask;

public sealed class UpdateAgentTaskCommandValidator : AbstractValidator<UpdateAgentTaskCommand>
{
    public UpdateAgentTaskCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Title"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Title", 200]);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Description", 2000]);
    }
}
