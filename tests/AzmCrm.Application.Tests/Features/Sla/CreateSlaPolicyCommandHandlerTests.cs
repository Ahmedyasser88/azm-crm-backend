using AzmCrm.Application.Features.Sla.Commands.CreateSlaPolicy;
using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Sla;

public class CreateSlaPolicyCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_policy_and_returns_id()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateSlaPolicyCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateSlaPolicyCommand("High priority", TicketPriority.High, 30, 240), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var policy = await dbContext.SlaPolicies.SingleAsync(p => p.Id == result.Data);
        Assert.Equal("High priority", policy.Name);
        Assert.Equal(TicketPriority.High, policy.Priority);
        Assert.Equal(30, policy.ResponseTimeMinutes);
        Assert.Equal(240, policy.ResolutionTimeMinutes);
        Assert.True(policy.IsActive);
    }

    [Fact]
    public async Task Create_with_active_priority_conflict_returns_failure()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.SlaPolicies.Add(new SlaPolicy
        {
            Name = "Existing",
            Priority = TicketPriority.High,
            ResponseTimeMinutes = 30,
            ResolutionTimeMinutes = 240
        });
        await dbContext.SaveChangesAsync();

        var handler = new CreateSlaPolicyCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateSlaPolicyCommand("Another", TicketPriority.High, 15, 120), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Create_with_inactive_priority_conflict_succeeds()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.SlaPolicies.Add(new SlaPolicy
        {
            Name = "Existing",
            Priority = TicketPriority.High,
            ResponseTimeMinutes = 30,
            ResolutionTimeMinutes = 240,
            IsActive = false
        });
        await dbContext.SaveChangesAsync();

        var handler = new CreateSlaPolicyCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateSlaPolicyCommand("Another", TicketPriority.High, 15, 120), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
