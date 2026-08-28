using AzmCrm.Application.Features.Customers.Commands.CreateCustomer;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class CreateCustomerCommandValidatorTests
{
    private readonly CreateCustomerCommandValidator _validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_FullName_fails()
    {
        var command = new CreateCustomerCommand("", null, null, null, null, null, null, null, null, null);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerCommand.FullName));
    }

    [Fact]
    public void Invalid_email_fails()
    {
        var command = new CreateCustomerCommand(
            "Jane Doe", null, "not-an-email", null, null, null, null, null, null, null);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerCommand.Email));
    }

    [Fact]
    public void Invalid_phone_number_fails()
    {
        var command = new CreateCustomerCommand(
            "Jane Doe", null, null, "123", null, null, null, null, null, null);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerCommand.PhoneNumber));
    }

    [Fact]
    public void Valid_command_passes()
    {
        var command = new CreateCustomerCommand(
            "Jane Doe", "Acme Inc", "jane@acme.com", "0501234567",
            "123 Main St", null, "Riyadh", "Riyadh Province", "12345", "SA");

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
