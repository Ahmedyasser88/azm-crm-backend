using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Commands.GenerateTicketSummary;

public sealed class GenerateTicketSummaryCommandValidator : AbstractValidator<GenerateTicketSummaryCommand>
{
    public GenerateTicketSummaryCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);
    }
}
