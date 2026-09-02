using AzmCrm.Application.Features.Dashboard.Queries.GetMyTickets;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Dashboard;

public class GetMyTicketsQueryValidatorTests
{
    private readonly GetMyTicketsQueryValidator _validator = new(new StubLocalizationService());

    [Fact]
    public void PageNumber_less_than_1_fails()
    {
        var result = _validator.Validate(new GetMyTicketsQuery(PageNumber: 0));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void PageSize_out_of_range_fails()
    {
        var result = _validator.Validate(new GetMyTicketsQuery(PageSize: 0));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_query_passes()
    {
        var result = _validator.Validate(new GetMyTicketsQuery());

        Assert.True(result.IsValid);
    }
}
