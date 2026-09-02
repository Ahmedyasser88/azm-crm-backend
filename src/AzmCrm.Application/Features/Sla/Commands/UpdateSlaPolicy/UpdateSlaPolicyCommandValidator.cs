using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Sla.Commands.UpdateSlaPolicy;

public sealed class UpdateSlaPolicyCommandValidator : AbstractValidator<UpdateSlaPolicyCommand>
{
    public UpdateSlaPolicyCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Name", 200]);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Priority"]);

        RuleFor(x => x.ResponseTimeMinutes)
            .GreaterThan(0)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Response Time (minutes)", 0]);

        RuleFor(x => x.ResolutionTimeMinutes)
            .GreaterThan(x => x.ResponseTimeMinutes)
            .WithMessage("Resolution Time (minutes) must be greater than Response Time (minutes).");
    }
}
