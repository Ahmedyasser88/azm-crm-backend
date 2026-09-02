using AzmCrm.Application.Features.Automation.Commands.UpdateEscalationRule;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Automation;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Automation;

public class UpdateEscalationRuleCommandHandlerTests
{
    [Fact]
    public async Task Update_persists_changes()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var rule = new EscalationRule { Name = "Original", OverdueMinutes = 0 };
        dbContext.EscalationRules.Add(rule);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateEscalationRuleCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpdateEscalationRuleCommand(rule.Id, "Updated", TicketPriority.High, 30, false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.EscalationRules.SingleAsync(r => r.Id == rule.Id);
        Assert.Equal("Updated", persisted.Name);
        Assert.Equal(TicketPriority.High, persisted.Priority);
        Assert.Equal(30, persisted.OverdueMinutes);
        Assert.False(persisted.IsActive);
    }

    [Fact]
    public async Task Update_missing_rule_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new UpdateEscalationRuleCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateEscalationRuleCommand(Guid.NewGuid(), "N", null, 0, true), CancellationToken.None));
    }
}
