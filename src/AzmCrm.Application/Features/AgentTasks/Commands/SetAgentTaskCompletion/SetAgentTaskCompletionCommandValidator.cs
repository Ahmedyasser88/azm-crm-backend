using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.AgentTasks.Commands.SetAgentTaskCompletion;

public sealed class SetAgentTaskCompletionCommandValidator : AbstractValidator<SetAgentTaskCompletionCommand>
{
    public SetAgentTaskCompletionCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);
    }
}
