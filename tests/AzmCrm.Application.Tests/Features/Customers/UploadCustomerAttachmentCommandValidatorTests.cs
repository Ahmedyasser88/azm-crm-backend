using AzmCrm.Application.Features.Customers.Commands.UploadCustomerAttachment;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class UploadCustomerAttachmentCommandValidatorTests
{
    private static UploadCustomerAttachmentCommandValidator CreateValidator(long maxFileSizeBytes = 10_485_760) =>
        new(new StubLocalizationService(), new StubFileStorageService { MaxFileSizeBytes = maxFileSizeBytes });

    [Fact]
    public void Zero_byte_file_fails()
    {
        var validator = CreateValidator();
        var command = new UploadCustomerAttachmentCommand(
            Guid.NewGuid(), "invoice.pdf", "application/pdf", 0, Stream.Null);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadCustomerAttachmentCommand.FileSizeBytes));
    }

    [Fact]
    public void File_over_MaxFileSizeBytes_fails()
    {
        var validator = CreateValidator(maxFileSizeBytes: 100);
        var command = new UploadCustomerAttachmentCommand(
            Guid.NewGuid(), "invoice.pdf", "application/pdf", 101, Stream.Null);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadCustomerAttachmentCommand.FileSizeBytes));
    }

    [Fact]
    public void Empty_FileName_fails()
    {
        var validator = CreateValidator();
        var command = new UploadCustomerAttachmentCommand(
            Guid.NewGuid(), "", "application/pdf", 10, Stream.Null);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadCustomerAttachmentCommand.FileName));
    }

    [Fact]
    public void Valid_command_passes()
    {
        var validator = CreateValidator();
        var command = new UploadCustomerAttachmentCommand(
            Guid.NewGuid(), "invoice.pdf", "application/pdf", 1024, Stream.Null);

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
