using AzmCrm.Application.Features.Automation.Commands.CreateAssignmentRule;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Automation;

public class CreateAssignmentRuleCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_rule_and_returns_id()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var agentId = Guid.NewGuid();
        var identity = new StubIdentityQueryService();
        identity.Users[agentId] = ("Agent Smith", "agent@azm.com");

        var handler = new CreateAssignmentRuleCommandHandler(dbContext, identity);

        var result = await handler.Handle(
            new CreateAssignmentRuleCommand("Billing to Agent", TicketCategory.Billing, null, agentId, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var rule = await dbContext.AssignmentRules.SingleAsync(r => r.Id == result.Data);
        Assert.Equal("Billing to Agent", rule.Name);
        Assert.Equal(TicketCategory.Billing, rule.Category);
        Assert.Null(rule.Priority);
        Assert.Equal(agentId, rule.AssignedToUserId);
        Assert.Equal(1, rule.EvaluationOrder);
    }

    [Fact]
    public async Task Create_with_unknown_agent_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateAssignmentRuleCommandHandler(dbContext, new StubIdentityQueryService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new CreateAssignmentRuleCommand("N", null, null, Guid.NewGuid(), 1), CancellationToken.None));
    }
}
