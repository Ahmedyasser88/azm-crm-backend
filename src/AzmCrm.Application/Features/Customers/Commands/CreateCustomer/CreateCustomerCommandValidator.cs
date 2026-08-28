using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator(ILocalizationService localization)
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Full Name"])
            .MaximumLength(200).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Full Name", 200]);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage(localization[LocalizationKeys.Validation.EmailInvalid])
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^(05\d{8}|\+9665\d{8}|\+?\d{10,15})$")
            .WithMessage(localization[LocalizationKeys.Validation.InvalidPhoneNumber])
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.CompanyName).MaximumLength(200)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Company Name", 200]);
        RuleFor(x => x.AddressLine1).MaximumLength(250)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Address Line 1", 250]);
        RuleFor(x => x.AddressLine2).MaximumLength(250)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Address Line 2", 250]);
        RuleFor(x => x.City).MaximumLength(100)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "City", 100]);
        RuleFor(x => x.State).MaximumLength(100)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "State", 100]);
        RuleFor(x => x.PostalCode).MaximumLength(20)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Postal Code", 20]);
        RuleFor(x => x.Country).MaximumLength(100)
            .WithMessage(localization[LocalizationKeys.Validation.MaxLength, "Country", 100]);
    }
}
