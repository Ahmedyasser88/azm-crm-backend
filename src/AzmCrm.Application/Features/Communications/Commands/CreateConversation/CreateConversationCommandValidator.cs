using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.CreateConversation;

public sealed class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Customer Id"]);

        RuleFor(x => x.Channel)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Channel"]);

        RuleFor(x => x.Subject)
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Subject", 200]);
    }
}
