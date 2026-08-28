using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.ReceiveInboundSms;

public sealed class ReceiveInboundSmsCommandValidator : AbstractValidator<ReceiveInboundSmsCommand>
{
    public ReceiveInboundSmsCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.FromPhoneNumber)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "From Phone Number"])
            .Matches(@"^(05\d{8}|\+9665\d{8}|\+?\d{10,15})$")
            .WithMessage(localization[LocalizationKeys.Validation.InvalidPhoneNumber]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
