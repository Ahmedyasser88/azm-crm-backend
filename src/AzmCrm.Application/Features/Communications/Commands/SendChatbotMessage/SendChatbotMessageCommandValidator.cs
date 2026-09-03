using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Communications.Commands.SendChatbotMessage;

public sealed class SendChatbotMessageCommandValidator : AbstractValidator<SendChatbotMessageCommand>
{
    public SendChatbotMessageCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Conversation Id"]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
