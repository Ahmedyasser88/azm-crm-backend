using AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticleStep;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.KnowledgeBase;

public class DeleteKnowledgeArticleStepCommandHandlerTests
{
    [Fact]
    public async Task Delete_soft_deletes_step()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var article = new KnowledgeArticle { Title = "T", Content = "C", Type = KnowledgeArticleType.Guide };
        dbContext.KnowledgeArticles.Add(article);
        var step = new KnowledgeArticleStep
        {
            KnowledgeArticleId = article.Id, StepNumber = 1, Title = "S", Description = "D"
        };
        dbContext.KnowledgeArticleSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var handler = new DeleteKnowledgeArticleStepCommandHandler(dbContext, new StubCurrentUserService());

        var result = await handler.Handle(
            new DeleteKnowledgeArticleStepCommand(article.Id, step.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.KnowledgeArticleSteps.IgnoreQueryFilters()
            .SingleAsync(s => s.Id == step.Id);
        Assert.True(persisted.IsDeleted);
    }

    [Fact]
    public async Task Delete_missing_step_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new DeleteKnowledgeArticleStepCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new DeleteKnowledgeArticleStepCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }
}
