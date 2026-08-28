using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomerInteraction;

public sealed class CreateCustomerInteractionCommandValidator : AbstractValidator<CreateCustomerInteractionCommand>
{
    public CreateCustomerInteractionCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Customer Id"]);

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage(localization[LocalizationKeys.Validation.InvalidValue, "Type"]);

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Subject"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Subject", 200]);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Description", 2000]);

        RuleFor(x => x.OccurredOn)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Occurred On"]);
    }
}
