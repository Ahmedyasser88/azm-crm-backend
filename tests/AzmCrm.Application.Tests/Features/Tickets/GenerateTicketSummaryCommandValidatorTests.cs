using AzmCrm.Application.Features.Tickets.Commands.GenerateTicketSummary;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class GenerateTicketSummaryCommandValidatorTests
{
    private static readonly GenerateTicketSummaryCommandValidator Validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_TicketId_fails()
    {
        var command = new GenerateTicketSummaryCommand(Guid.Empty);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_TicketId_passes()
    {
        var command = new GenerateTicketSummaryCommand(Guid.NewGuid());

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
