using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.QuickReplies.Commands.DeleteQuickReplyTemplate;

public sealed class DeleteQuickReplyTemplateCommandValidator : AbstractValidator<DeleteQuickReplyTemplateCommand>
{
    public DeleteQuickReplyTemplateCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);
    }
}
