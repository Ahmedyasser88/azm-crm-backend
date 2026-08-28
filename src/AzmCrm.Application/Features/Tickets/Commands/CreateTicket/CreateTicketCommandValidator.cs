using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Tickets.Commands.CreateTicket;

public sealed class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Customer Id"]);

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
