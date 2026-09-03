using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Queries.SuggestKnowledgeArticlesForTicket;

public sealed class SuggestKnowledgeArticlesForTicketQueryValidator
    : AbstractValidator<SuggestKnowledgeArticlesForTicketQuery>
{
    public SuggestKnowledgeArticlesForTicketQueryValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);

        RuleFor(x => x.MaxResults)
            .InclusiveBetween(1, 20)
            .WithMessage("Max Results must be between 1 and 20.");
    }
}
