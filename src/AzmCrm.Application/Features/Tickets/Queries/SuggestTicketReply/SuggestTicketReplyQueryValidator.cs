using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Queries.SuggestTicketReply;

public sealed class SuggestTicketReplyQueryValidator : AbstractValidator<SuggestTicketReplyQuery>
{
    public SuggestTicketReplyQueryValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);
    }
}
