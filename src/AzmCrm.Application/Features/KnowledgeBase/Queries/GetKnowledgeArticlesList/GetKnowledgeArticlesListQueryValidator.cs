using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetKnowledgeArticlesList;

public sealed class GetKnowledgeArticlesListQueryValidator : AbstractValidator<GetKnowledgeArticlesListQuery>
{
    public GetKnowledgeArticlesListQueryValidator(ILocalizationService localization)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Page Number", 0]);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page Size must be between 1 and 100.");
    }
}
