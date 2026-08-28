using AzmCrm.Application.Features.Tickets.Commands.EscalateTicket;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class EscalateTicketCommandValidatorTests
{
    private static readonly EscalateTicketCommandValidator Validator = new(new StubLocalizationService());

    [Fact]
    public void Reason_over_1000_chars_fails()
    {
        var command = new EscalateTicketCommand(Guid.NewGuid(), new string('x', 1001));

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Null_Reason_passes()
    {
        var command = new EscalateTicketCommand(Guid.NewGuid(), null);

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var command = new EscalateTicketCommand(Guid.NewGuid(), "SLA breach imminent");

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
