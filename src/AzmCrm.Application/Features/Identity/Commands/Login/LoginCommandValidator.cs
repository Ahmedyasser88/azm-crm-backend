using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Identity.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Username or Email"]);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Password"]);
    }
}
