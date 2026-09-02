using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.KnowledgeBase.Queries.GetPublishedKnowledgeArticleById;

public sealed class GetPublishedKnowledgeArticleByIdQueryValidator
    : AbstractValidator<GetPublishedKnowledgeArticleByIdQuery>
{
    public GetPublishedKnowledgeArticleByIdQueryValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);
    }
}
