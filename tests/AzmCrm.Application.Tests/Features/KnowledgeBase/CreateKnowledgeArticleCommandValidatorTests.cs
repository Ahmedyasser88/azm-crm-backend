using AzmCrm.Application.Features.KnowledgeBase.Commands.CreateKnowledgeArticle;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.KnowledgeBase;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class CreateKnowledgeArticleCommandValidatorTests
{
    private readonly CreateKnowledgeArticleCommandValidator _validator = new(new StubLocalizationService());

    [Fact]
    public void Undefined_Type_fails()
    {
        var result = _validator.Validate(
            new CreateKnowledgeArticleCommand("T", "C", (KnowledgeArticleType)999, null, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Title_exceeding_300_chars_fails()
    {
        var result = _validator.Validate(
            new CreateKnowledgeArticleCommand(new string('a', 301), "C", KnowledgeArticleType.Faq, null, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Content_exceeding_8000_chars_fails()
    {
        var result = _validator.Validate(
            new CreateKnowledgeArticleCommand("T", new string('a', 8001), KnowledgeArticleType.Faq, null, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = _validator.Validate(
            new CreateKnowledgeArticleCommand("T", "C", KnowledgeArticleType.Faq, "Category", "tag1,tag2"));

        Assert.True(result.IsValid);
    }
}
