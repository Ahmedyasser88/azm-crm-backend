using AzmCrm.Application.Features.Customers.Commands.CreateCustomerInteraction;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class CreateCustomerInteractionCommandValidatorTests
{
    private readonly CreateCustomerInteractionCommandValidator _validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_Subject_fails()
    {
        var command = new CreateCustomerInteractionCommand(
            Guid.NewGuid(), InteractionType.Call, "", null, DateTime.UtcNow);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerInteractionCommand.Subject));
    }

    [Fact]
    public void Undefined_enum_value_fails()
    {
        // The JSON layer's JsonStringEnumConverter would reject an invalid string before this
        // command is ever constructed from an HTTP request; this exercises IsInEnum() directly
        // as the defense-in-depth check for a caller that bypasses the JSON binder.
        var command = new CreateCustomerInteractionCommand(
            Guid.NewGuid(), (InteractionType)999, "Subject", null, DateTime.UtcNow);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerInteractionCommand.Type));
    }

    [Fact]
    public void Valid_command_passes()
    {
        var command = new CreateCustomerInteractionCommand(
            Guid.NewGuid(), InteractionType.Meeting, "Renewal discussion", "Details", DateTime.UtcNow);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
