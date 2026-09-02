using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UnpublishKnowledgeArticle;

public sealed class UnpublishKnowledgeArticleCommandValidator : AbstractValidator<UnpublishKnowledgeArticleCommand>
{
    public UnpublishKnowledgeArticleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);
    }
}
