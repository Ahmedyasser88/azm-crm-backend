using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplatesList;

public sealed class GetQuickReplyTemplatesListQueryValidator : AbstractValidator<GetQuickReplyTemplatesListQuery>
{
    public GetQuickReplyTemplatesListQueryValidator(ILocalizationService localization)
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Page Number", 0]);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page Size must be between 1 and 100.");
    }
}
