using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandValidator : AbstractValidator<EscalateTicketCommand>
{
    public EscalateTicketCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);

        RuleFor(x => x.Reason)
            .MaximumLength(1000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Reason", 1000]);
    }
}
