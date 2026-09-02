using AzmCrm.Application.Features.KnowledgeBase.Commands.AddKnowledgeArticleStep;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class AddKnowledgeArticleStepCommandHandlerTests
{
    [Fact]
    public async Task Add_persists_step_and_returns_id()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle { Title = "T", Content = "C", Type = KnowledgeArticleType.Guide };
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync();

        var handler = new AddKnowledgeArticleStepCommandHandler(dbContext);

        var result = await handler.Handle(
            new AddKnowledgeArticleStepCommand(article.Id, 1, "Step title", "Step description"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var step = await dbContext.KnowledgeArticleSteps.SingleAsync(s => s.Id == result.Data);
        Assert.Equal(article.Id, step.KnowledgeArticleId);
        Assert.Equal(1, step.StepNumber);
        Assert.Equal("Step title", step.Title);
    }

    [Fact]
    public async Task Add_to_missing_article_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new AddKnowledgeArticleStepCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new AddKnowledgeArticleStepCommand(Guid.NewGuid(), 1, "T", "D"), CancellationToken.None));
    }
}
