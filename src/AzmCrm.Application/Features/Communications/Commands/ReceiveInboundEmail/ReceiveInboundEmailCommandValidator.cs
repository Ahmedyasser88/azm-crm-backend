using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundEmail;

public sealed class ReceiveInboundEmailCommandValidator : AbstractValidator<ReceiveInboundEmailCommand>
{
    public ReceiveInboundEmailCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.FromEmail)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "From Email"])
            .EmailAddress().WithMessage(localization[LocalizationKeys.Validation.EmailInvalid]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
