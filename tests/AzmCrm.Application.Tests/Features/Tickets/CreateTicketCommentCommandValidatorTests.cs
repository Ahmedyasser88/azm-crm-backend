using AzmCrm.Application.Features.Tickets.Commands.CreateTicketComment;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class CreateTicketCommentCommandValidatorTests
{
    private readonly CreateTicketCommentCommandValidator _validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_TicketId_fails()
    {
        var result = _validator.Validate(new CreateTicketCommentCommand(Guid.Empty, "Content"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_Content_fails()
    {
        var result = _validator.Validate(new CreateTicketCommentCommand(Guid.NewGuid(), ""));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Content_over_4000_chars_fails()
    {
        var result = _validator.Validate(new CreateTicketCommentCommand(Guid.NewGuid(), new string('a', 4001)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = _validator.Validate(new CreateTicketCommentCommand(Guid.NewGuid(), "Content"));

        Assert.True(result.IsValid);
    }
}
