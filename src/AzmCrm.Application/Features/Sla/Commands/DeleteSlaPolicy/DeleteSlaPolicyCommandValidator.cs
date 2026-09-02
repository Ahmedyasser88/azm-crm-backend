using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Sla.Commands.DeleteSlaPolicy;

public sealed class DeleteSlaPolicyCommandValidator : AbstractValidator<DeleteSlaPolicyCommand>
{
    public DeleteSlaPolicyCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);
    }
}
