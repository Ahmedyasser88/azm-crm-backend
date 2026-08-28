using AzmCrm.Application.Features.Tickets.Commands.AssignTicket;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class AssignTicketCommandValidatorTests
{
    private static readonly AssignTicketCommandValidator Validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_TicketId_fails()
    {
        var command = new AssignTicketCommand(Guid.Empty, Guid.NewGuid());

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Null_AssignedToUserId_passes()
    {
        var command = new AssignTicketCommand(Guid.NewGuid(), null);

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var command = new AssignTicketCommand(Guid.NewGuid(), Guid.NewGuid());

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
