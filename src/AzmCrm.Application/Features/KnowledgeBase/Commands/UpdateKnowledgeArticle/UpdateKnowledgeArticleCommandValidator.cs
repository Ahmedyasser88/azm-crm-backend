using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticle;

public sealed class UpdateKnowledgeArticleCommandValidator : AbstractValidator<UpdateKnowledgeArticleCommand>
{
    public UpdateKnowledgeArticleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Title"])
            .MaximumLength(300).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Title", 300]);

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Content"])
            .MaximumLength(8000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Content", 8000]);

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Type"]);

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Category", 100]);

        RuleFor(x => x.Tags)
            .MaximumLength(500).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Tags", 500]);
    }
}
