using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.QuickReplies.Commands.CreateQuickReplyTemplate;

public sealed class CreateQuickReplyTemplateCommandValidator : AbstractValidator<CreateQuickReplyTemplateCommand>
{
    public CreateQuickReplyTemplateCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Title"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Title", 200]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Body"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Body", 4000]);
    }
}
