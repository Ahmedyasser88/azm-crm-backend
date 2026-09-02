using AzmCrm.Application.Features.KnowledgeBase.Queries.SearchKnowledgeArticles;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class SearchKnowledgeArticlesQueryValidatorTests
{
    private readonly SearchKnowledgeArticlesQueryValidator _validator = new(new StubLocalizationService());

    [Fact]
    public void Empty_Query_fails()
    {
        var result = _validator.Validate(new SearchKnowledgeArticlesQuery(""));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Whitespace_Query_fails()
    {
        var result = _validator.Validate(new SearchKnowledgeArticlesQuery("   "));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_Query_passes()
    {
        var result = _validator.Validate(new SearchKnowledgeArticlesQuery("password"));

        Assert.True(result.IsValid);
    }
}
