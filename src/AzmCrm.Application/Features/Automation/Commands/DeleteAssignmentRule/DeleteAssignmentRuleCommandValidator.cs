using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Automation.Commands.DeleteAssignmentRule;

public sealed class DeleteAssignmentRuleCommandValidator : AbstractValidator<DeleteAssignmentRuleCommand>
{
    public DeleteAssignmentRuleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);
    }
}
