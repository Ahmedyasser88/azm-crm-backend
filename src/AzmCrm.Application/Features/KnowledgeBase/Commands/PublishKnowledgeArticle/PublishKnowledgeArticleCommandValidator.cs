using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.PublishKnowledgeArticle;

public sealed class PublishKnowledgeArticleCommandValidator : AbstractValidator<PublishKnowledgeArticleCommand>
{
    public PublishKnowledgeArticleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);
    }
}
