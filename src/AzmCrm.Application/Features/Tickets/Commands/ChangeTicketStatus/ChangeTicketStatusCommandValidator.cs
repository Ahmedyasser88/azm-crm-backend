using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Commands.ChangeTicketStatus;

public sealed class ChangeTicketStatusCommandValidator : AbstractValidator<ChangeTicketStatusCommand>
{
    public ChangeTicketStatusCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Ticket Id"]);

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Status"]);
    }
}
