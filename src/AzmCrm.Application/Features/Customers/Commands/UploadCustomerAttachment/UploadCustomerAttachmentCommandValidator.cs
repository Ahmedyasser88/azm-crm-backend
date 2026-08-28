using AzmCrm.Application.Localization;
using AzmCrm.Application.Shared.Interfaces;
using FluentValidation;

namespace AzmCrm.Application.Features.Customers.Commands.UploadCustomerAttachment;

public sealed class UploadCustomerAttachmentCommandValidator : AbstractValidator<UploadCustomerAttachmentCommand>
{
    public UploadCustomerAttachmentCommandValidator(
        ILocalizationService localization, IFileStorageService fileStorage)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "Customer Id"]);

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage(localization[LocalizationKeys.Validation.Required, "File Name"])
            .MaximumLength(255).WithMessage(localization[LocalizationKeys.Validation.MaxLength, "File Name", 255]);

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
                .WithMessage(localization[LocalizationKeys.Validation.MustBeGreaterThan, "File Size", 0])
            .LessThanOrEqualTo(fileStorage.MaxFileSizeBytes)
                .WithMessage(localization[
                    LocalizationKeys.Validation.FileTooLarge, fileStorage.MaxFileSizeBytes / 1_048_576]);
    }
}
