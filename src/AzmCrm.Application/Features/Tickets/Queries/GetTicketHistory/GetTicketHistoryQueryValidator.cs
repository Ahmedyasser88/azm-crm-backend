using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Queries.GetTicketHistory;

public sealed class GetTicketHistoryQueryValidator : AbstractValidator<GetTicketHistoryQuery>
{
    public GetTicketHistoryQueryValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Page Number", 0]);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page Size must be between 1 and 100.");
    }
}
