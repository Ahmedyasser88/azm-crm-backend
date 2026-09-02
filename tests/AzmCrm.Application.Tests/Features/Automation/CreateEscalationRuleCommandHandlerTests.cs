using AzmCrm.Application.Features.Automation.Commands.CreateEscalationRule;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Automation;

public class CreateEscalationRuleCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_rule_and_returns_id()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateEscalationRuleCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateEscalationRuleCommand("Urgent overdue", TicketPriority.Urgent, 15), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var rule = await dbContext.EscalationRules.SingleAsync(r => r.Id == result.Data);
        Assert.Equal("Urgent overdue", rule.Name);
        Assert.Equal(TicketPriority.Urgent, rule.Priority);
        Assert.Equal(15, rule.OverdueMinutes);
        Assert.True(rule.IsActive);
    }
}
