using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.AgentTasks.Commands.DeleteAgentTask;

public sealed class DeleteAgentTaskCommandValidator : AbstractValidator<DeleteAgentTaskCommand>
{
    public DeleteAgentTaskCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);
    }
}
