using AzmCrm.Application.Features.Automation.Commands.DeleteEscalationRule;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Automation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Automation;

public class DeleteEscalationRuleCommandHandlerTests
{
    [Fact]
    public async Task Delete_soft_deletes_rule()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var rule = new EscalationRule { Name = "N", OverdueMinutes = 0 };
        dbContext.EscalationRules.Add(rule);
        await dbContext.SaveChangesAsync();

        var handler = new DeleteEscalationRuleCommandHandler(dbContext, new StubCurrentUserService());

        var result = await handler.Handle(new DeleteEscalationRuleCommand(rule.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.EscalationRules.IgnoreQueryFilters().SingleAsync(r => r.Id == rule.Id);
        Assert.True(persisted.IsDeleted);
    }

    [Fact]
    public async Task Delete_missing_rule_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new DeleteEscalationRuleCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new DeleteEscalationRuleCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
