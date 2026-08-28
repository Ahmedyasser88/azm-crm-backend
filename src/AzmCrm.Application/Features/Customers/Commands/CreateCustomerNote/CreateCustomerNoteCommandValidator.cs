using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerNote;

public sealed class CreateCustomerNoteCommandValidator : AbstractValidator<CreateCustomerNoteCommand>
{
    public CreateCustomerNoteCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Customer Id"]);

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Content"])
            .MaximumLength(4000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Content", 4000]);
    }
}
