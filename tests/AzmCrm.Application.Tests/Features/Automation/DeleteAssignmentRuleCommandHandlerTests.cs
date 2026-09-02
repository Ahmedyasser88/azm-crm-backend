using AzmCrm.Application.Features.Automation.Commands.DeleteAssignmentRule;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Automation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Automation;

public class DeleteAssignmentRuleCommandHandlerTests
{
    [Fact]
    public async Task Delete_soft_deletes_rule()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var rule = new AssignmentRule { Name = "N", AssignedToUserId = Guid.NewGuid(), EvaluationOrder = 1 };
        dbContext.AssignmentRules.Add(rule);
        await dbContext.SaveChangesAsync();

        var handler = new DeleteAssignmentRuleCommandHandler(dbContext, new StubCurrentUserService());

        var result = await handler.Handle(new DeleteAssignmentRuleCommand(rule.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.AssignmentRules.IgnoreQueryFilters().SingleAsync(r => r.Id == rule.Id);
        Assert.True(persisted.IsDeleted);
    }

    [Fact]
    public async Task Delete_missing_rule_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new DeleteAssignmentRuleCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new DeleteAssignmentRuleCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
