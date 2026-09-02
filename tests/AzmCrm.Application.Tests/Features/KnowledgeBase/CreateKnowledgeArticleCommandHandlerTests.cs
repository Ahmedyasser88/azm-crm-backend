using AzmCrm.Application.Features.KnowledgeBase.Commands.CreateKnowledgeArticle;
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class CreateKnowledgeArticleCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_article_as_Draft_and_returns_id()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateKnowledgeArticleCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateKnowledgeArticleCommand(
                "How do I reset my password?", "Go to Settings > Security > Reset Password.",
                KnowledgeArticleType.Faq, "Account", "password,reset"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var article = await dbContext.KnowledgeArticles.SingleAsync(a => a.Id == result.Data);
        Assert.Equal("How do I reset my password?", article.Title);
        Assert.Equal(KnowledgeArticleType.Faq, article.Type);
        Assert.Equal(KnowledgeArticleStatus.Draft, article.Status);
        Assert.Null(article.PublishedOn);
        Assert.Null(article.PublishedBy);
    }

    [Fact]
    public async Task Create_with_null_Category_and_Tags_succeeds()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateKnowledgeArticleCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateKnowledgeArticleCommand("Title", "Content", KnowledgeArticleType.Article, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var article = await dbContext.KnowledgeArticles.SingleAsync(a => a.Id == result.Data);
        Assert.Null(article.Category);
        Assert.Null(article.Tags);
    }
}
