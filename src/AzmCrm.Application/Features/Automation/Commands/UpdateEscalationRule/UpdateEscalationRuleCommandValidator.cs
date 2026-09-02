using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Automation.Commands.UpdateEscalationRule;

public sealed class UpdateEscalationRuleCommandValidator : AbstractValidator<UpdateEscalationRuleCommand>
{
    public UpdateEscalationRuleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);

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
