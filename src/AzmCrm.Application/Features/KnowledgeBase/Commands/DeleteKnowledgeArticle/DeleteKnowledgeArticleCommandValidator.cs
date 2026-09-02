using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticle;

public sealed class DeleteKnowledgeArticleCommandValidator : AbstractValidator<DeleteKnowledgeArticleCommand>
{
    public DeleteKnowledgeArticleCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);
    }
}
