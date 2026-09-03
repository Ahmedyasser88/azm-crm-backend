using AzmCrm.Application.Features.Tickets.Queries.SuggestTicketReply;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class SuggestTicketReplyQueryValidatorTests
{
    private static readonly SuggestTicketReplyQueryValidator Validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_TicketId_fails()
    {
        var query = new SuggestTicketReplyQuery(Guid.Empty);

        var result = Validator.Validate(query);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_TicketId_passes()
    {
        var query = new SuggestTicketReplyQuery(Guid.NewGuid());

        var result = Validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
