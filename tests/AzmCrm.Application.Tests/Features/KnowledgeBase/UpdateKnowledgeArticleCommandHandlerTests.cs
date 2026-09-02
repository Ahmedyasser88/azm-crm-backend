using AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticle;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class UpdateKnowledgeArticleCommandHandlerTests
{
    [Fact]
    public async Task Update_persists_changes()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle
        {
            Title = "Old title",
            Content = "Old content",
            Type = KnowledgeArticleType.Faq
        };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateKnowledgeArticleCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpdateKnowledgeArticleCommand(
                article.Id, "New title", "New content", KnowledgeArticleType.Guide, "Billing", "invoice"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.KnowledgeArticles.SingleAsync(a => a.Id == article.Id);
        Assert.Equal("New title", persisted.Title);
        Assert.Equal("New content", persisted.Content);
        Assert.Equal(KnowledgeArticleType.Guide, persisted.Type);
        Assert.Equal("Billing", persisted.Category);
        Assert.Equal("invoice", persisted.Tags);
    }

    [Fact]
    public async Task Update_missing_article_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new UpdateKnowledgeArticleCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateKnowledgeArticleCommand(Guid.NewGuid(), "T", "C", KnowledgeArticleType.Faq, null, null),
            CancellationToken.None));
    }
}
