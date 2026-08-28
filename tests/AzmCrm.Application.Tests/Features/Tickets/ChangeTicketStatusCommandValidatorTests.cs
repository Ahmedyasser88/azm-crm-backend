using AzmCrm.Application.Features.Tickets.Commands.ChangeTicketStatus;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class ChangeTicketStatusCommandValidatorTests
{
    private static readonly ChangeTicketStatusCommandValidator Validator = new(new StubLocalizationService());

    [Fact]
    public void Undefined_Status_fails()
    {
        var command = new ChangeTicketStatusCommand(Guid.NewGuid(), (TicketStatus)999);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var command = new ChangeTicketStatusCommand(Guid.NewGuid(), TicketStatus.Open);

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
