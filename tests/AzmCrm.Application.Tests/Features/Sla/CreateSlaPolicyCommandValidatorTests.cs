using AzmCrm.Application.Features.Sla.Commands.CreateSlaPolicy;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Sla;

public class CreateSlaPolicyCommandValidatorTests
{
    private readonly CreateSlaPolicyCommandValidator _validator = new(new StubLocalizationService());

    [Fact]
    public void ResolutionTimeMinutes_not_greater_than_ResponseTimeMinutes_fails()
    {
        var result = _validator.Validate(new CreateSlaPolicyCommand("N", TicketPriority.Low, 60, 60));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Undefined_Priority_fails()
    {
        var result = _validator.Validate(new CreateSlaPolicyCommand("N", (TicketPriority)999, 30, 240));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = _validator.Validate(new CreateSlaPolicyCommand("N", TicketPriority.Low, 30, 240));

        Assert.True(result.IsValid);
    }
}
