using AzmCrm.Application.Features.Tickets.Commands.CreateTicket;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class CreateTicketCommandValidatorTests
{
    private static readonly CreateTicketCommandValidator Validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_Title_fails()
    {
        var command = new CreateTicketCommand(Guid.NewGuid(), "", null, TicketCategory.General, TicketPriority.Low);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Undefined_Category_fails()
    {
        var command = new CreateTicketCommand(Guid.NewGuid(), "Title", null, (TicketCategory)999, TicketPriority.Low);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Undefined_Priority_fails()
    {
        var command = new CreateTicketCommand(Guid.NewGuid(), "Title", null, TicketCategory.General, (TicketPriority)999);

        var result = Validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var command = new CreateTicketCommand(
            Guid.NewGuid(), "Title", "Description", TicketCategory.General, TicketPriority.Low);

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Null_Category_passes()
    {
        // Category is optional as of Story 27 (KAN-7) — omitting it triggers AI auto-categorization
        // in CreateTicketCommandHandler rather than failing validation.
        var command = new CreateTicketCommand(Guid.NewGuid(), "Title", null, null, TicketPriority.Low);

        var result = Validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
