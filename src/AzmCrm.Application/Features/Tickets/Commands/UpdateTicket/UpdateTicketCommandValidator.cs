using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Commands.UpdateTicket;

public sealed class UpdateTicketCommandValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Id"]);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Title"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Title", 200]);

        RuleFor(x => x.Description)
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Description", 4000]);

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Category"]);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Priority"]);
    }
}
