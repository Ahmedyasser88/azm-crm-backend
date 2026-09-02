using AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticleStep;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class UpdateKnowledgeArticleStepCommandHandlerTests
{
    [Fact]
    public async Task Update_persists_changes()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle { Title = "T", Content = "C", Type = KnowledgeArticleType.Guide };
        dbContext.KnowledgeArticles.Add(article);
        var step = new KnowledgeArticleStep
        {
            KnowledgeArticleId = article.Id, StepNumber = 1, Title = "Old", Description = "Old desc"
        };
        dbContext.KnowledgeArticleSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateKnowledgeArticleStepCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpdateKnowledgeArticleStepCommand(article.Id, step.Id, 2, "New", "New desc"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.KnowledgeArticleSteps.SingleAsync(s => s.Id == step.Id);
        Assert.Equal(2, persisted.StepNumber);
        Assert.Equal("New", persisted.Title);
        Assert.Equal("New desc", persisted.Description);
    }

    [Fact]
    public async Task Update_missing_step_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new UpdateKnowledgeArticleStepCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateKnowledgeArticleStepCommand(Guid.NewGuid(), Guid.NewGuid(), 1, "T", "D"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Update_step_belonging_to_different_article_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var articleA = new KnowledgeArticle { Title = "A", Content = "C", Type = KnowledgeArticleType.Guide };
        var articleB = new KnowledgeArticle { Title = "B", Content = "C", Type = KnowledgeArticleType.Guide };
        dbContext.KnowledgeArticles.AddRange(articleA, articleB);
        var step = new KnowledgeArticleStep
        {
            KnowledgeArticleId = articleA.Id, StepNumber = 1, Title = "S", Description = "D"
        };
        dbContext.KnowledgeArticleSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateKnowledgeArticleStepCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateKnowledgeArticleStepCommand(articleB.Id, step.Id, 1, "T", "D"), CancellationToken.None));
    }
}
