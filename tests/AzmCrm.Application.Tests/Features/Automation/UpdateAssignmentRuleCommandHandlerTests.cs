using AzmCrm.Application.Features.Automation.Commands.UpdateAssignmentRule;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Automation;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Automation;

public class UpdateAssignmentRuleCommandHandlerTests
{
    [Fact]
    public async Task Update_persists_changes()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var oldAgent = Guid.NewGuid();
        var newAgent = Guid.NewGuid();
        var rule = new AssignmentRule { Name = "Original", AssignedToUserId = oldAgent, EvaluationOrder = 1 };
        dbContext.AssignmentRules.Add(rule);
        await dbContext.SaveChangesAsync();

        var identity = new StubIdentityQueryService();
        identity.Users[newAgent] = ("New Agent", "new@azm.com");

        var handler = new UpdateAssignmentRuleCommandHandler(dbContext, identity);

        var result = await handler.Handle(
            new UpdateAssignmentRuleCommand(
                rule.Id, "Updated", TicketCategory.Technical, TicketPriority.High, newAgent, 2, false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.AssignmentRules.SingleAsync(r => r.Id == rule.Id);
        Assert.Equal("Updated", persisted.Name);
        Assert.Equal(TicketCategory.Technical, persisted.Category);
        Assert.Equal(TicketPriority.High, persisted.Priority);
        Assert.Equal(newAgent, persisted.AssignedToUserId);
        Assert.Equal(2, persisted.EvaluationOrder);
        Assert.False(persisted.IsActive);
    }

    [Fact]
    public async Task Update_missing_rule_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new UpdateAssignmentRuleCommandHandler(dbContext, new StubIdentityQueryService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateAssignmentRuleCommand(Guid.NewGuid(), "N", null, null, Guid.NewGuid(), 1, true),
            CancellationToken.None));
    }

    [Fact]
    public async Task Update_with_unknown_agent_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var rule = new AssignmentRule { Name = "N", AssignedToUserId = Guid.NewGuid(), EvaluationOrder = 1 };
        dbContext.AssignmentRules.Add(rule);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateAssignmentRuleCommandHandler(dbContext, new StubIdentityQueryService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateAssignmentRuleCommand(rule.Id, "N", null, null, Guid.NewGuid(), 1, true),
            CancellationToken.None));
    }
}
