using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Automation.Commands.DeleteEscalationRule;

public sealed class DeleteEscalationRuleCommandValidator : AbstractValidator<DeleteEscalationRuleCommand>
{
    public DeleteEscalationRuleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);
    }
}
