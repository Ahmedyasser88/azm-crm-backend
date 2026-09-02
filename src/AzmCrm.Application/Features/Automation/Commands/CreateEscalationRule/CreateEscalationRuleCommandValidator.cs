using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Automation.Commands.CreateEscalationRule;

public sealed class CreateEscalationRuleCommandValidator : AbstractValidator<CreateEscalationRuleCommand>
{
    public CreateEscalationRuleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Name", 200]);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Priority"])
            .When(x => x.Priority is not null);

        RuleFor(x => x.OverdueMinutes)
            .GreaterThanOrEqualTo(0)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Overdue Minutes", -1]);
    }
}
