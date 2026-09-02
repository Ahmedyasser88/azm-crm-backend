using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Automation.Commands.CreateAssignmentRule;

public sealed class CreateAssignmentRuleCommandValidator : AbstractValidator<CreateAssignmentRuleCommand>
{
    public CreateAssignmentRuleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Name", 200]);

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Category"])
            .When(x => x.Category is not null);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Priority"])
            .When(x => x.Priority is not null);

        RuleFor(x => x.AssignedToUserId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Assigned To User Id"]);
    }
}
