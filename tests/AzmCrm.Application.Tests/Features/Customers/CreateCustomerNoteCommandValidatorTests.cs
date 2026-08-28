using AzmCrm.Application.Features.Customers.Commands.CreateCustomerNote;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Customers;

public class CreateCustomerNoteCommandValidatorTests
{
    private readonly CreateCustomerNoteCommandValidator _validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_Content_fails()
    {
        var command = new CreateCustomerNoteCommand(Guid.NewGuid(), "");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerNoteCommand.Content));
    }

    [Fact]
    public void Content_over_4000_chars_fails()
    {
        var command = new CreateCustomerNoteCommand(Guid.NewGuid(), new string('a', 4001));

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerNoteCommand.Content));
    }

    [Fact]
    public void Valid_command_passes()
    {
        var command = new CreateCustomerNoteCommand(Guid.NewGuid(), "Called about renewal");

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
