using AzmCrm.Application.Features.Tickets.Queries.SuggestKnowledgeArticlesForTicket;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class SuggestKnowledgeArticlesForTicketQueryValidatorTests
{
    private static readonly SuggestKnowledgeArticlesForTicketQueryValidator Validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_TicketId_fails()
    {
        var query = new SuggestKnowledgeArticlesForTicketQuery(Guid.Empty);

        var result = Validator.Validate(query);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MaxResults_below_1_fails()
    {
        var query = new SuggestKnowledgeArticlesForTicketQuery(Guid.NewGuid(), MaxResults: 0);

        var result = Validator.Validate(query);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MaxResults_above_20_fails()
    {
        var query = new SuggestKnowledgeArticlesForTicketQuery(Guid.NewGuid(), MaxResults: 21);

        var result = Validator.Validate(query);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_request_passes()
    {
        var query = new SuggestKnowledgeArticlesForTicketQuery(Guid.NewGuid(), MaxResults: 5);

        var result = Validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
