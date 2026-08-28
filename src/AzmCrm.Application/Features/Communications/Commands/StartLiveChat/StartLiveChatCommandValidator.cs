using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.StartLiveChat;

public sealed class StartLiveChatCommandValidator : AbstractValidator<StartLiveChatCommand>
{
    public StartLiveChatCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Name", 200]);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Email"])
            .EmailAddress().WithMessage(localization[LocalizationKeys.Validation.EmailInvalid]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
