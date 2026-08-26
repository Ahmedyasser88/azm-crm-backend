using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Identity.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Username"])
            .MinimumLength(3).WithMessage(localization[LocalizationKeys.Validation.MinLength, "Username", 3])
            .MaximumLength(50).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Username", 50])
            .Matches("^[a-zA-Z0-9_-]+$").WithMessage(localization[LocalizationKeys.Validation.UsernamePattern]);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Email"])
            .EmailAddress().WithMessage(localization[LocalizationKeys.Validation.EmailInvalid])
            .MaximumLength(100).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Email", 100]);

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Mobile Number"])
            .Matches(@"^(05\d{8}|\+9665\d{8}|\+?\d{10,15})$").WithMessage(localization[LocalizationKeys.Validation.InvalidPhoneNumber]);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Password"])
            .MinimumLength(8).WithMessage(localization[LocalizationKeys.Validation.MinLength, "Password", 8])
            .Matches(@"[A-Z]").WithMessage(localization[LocalizationKeys.Validation.PasswordTooWeak])
            .Matches(@"[a-z]").WithMessage(localization[LocalizationKeys.Validation.PasswordTooWeak])
            .Matches(@"[0-9]").WithMessage(localization[LocalizationKeys.Validation.PasswordTooWeak])
            .Matches(@"[@$!%*?&#]").WithMessage(localization[LocalizationKeys.Validation.PasswordTooWeak]);

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage(localization[LocalizationKeys.Validation.PasswordsDoNotMatch]);
    }
}
