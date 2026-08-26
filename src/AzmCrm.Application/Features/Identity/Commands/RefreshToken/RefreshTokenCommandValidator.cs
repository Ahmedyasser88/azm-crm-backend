using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Identity.Commands.RefreshToken;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Refresh token"]);
    }
}
