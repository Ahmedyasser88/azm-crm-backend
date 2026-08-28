using AzmCrm.Application.Localization;
using FluentValidation;

namespace AzmCrm.Application.Features.Customers.Queries.GetCustomerAttachments;

public sealed class GetCustomerAttachmentsQueryValidator : AbstractValidator<GetCustomerAttachmentsQuery>
{
    public GetCustomerAttachmentsQueryValidator(ILocalizationService localization)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Customer Id"]);

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "Page Number", 0]);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page Size must be between 1 and 100.");
    }
}
