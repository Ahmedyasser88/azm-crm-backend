using AzmCrm.Application.Features.Sla.Commands.UpdateSlaPolicy;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Sla;

public class UpdateSlaPolicyCommandHandlerTests
{
    [Fact]
    public async Task Update_persists_changes()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var policy = new SlaPolicy
        {
            Name = "Original",
            Priority = TicketPriority.Low,
            ResponseTimeMinutes = 60,
            ResolutionTimeMinutes = 480
        };
        dbContext.SlaPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateSlaPolicyCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpdateSlaPolicyCommand(policy.Id, "Updated", TicketPriority.Medium, 45, 300, false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.SlaPolicies.SingleAsync(p => p.Id == policy.Id);
        Assert.Equal("Updated", persisted.Name);
        Assert.Equal(TicketPriority.Medium, persisted.Priority);
        Assert.Equal(45, persisted.ResponseTimeMinutes);
        Assert.Equal(300, persisted.ResolutionTimeMinutes);
        Assert.False(persisted.IsActive);
    }

    [Fact]
    public async Task Update_missing_policy_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new UpdateSlaPolicyCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateSlaPolicyCommand(Guid.NewGuid(), "X", TicketPriority.Low, 60, 480, true),
            CancellationToken.None));
    }

    [Fact]
    public async Task Update_to_active_with_priority_conflict_returns_failure()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var existing = new SlaPolicy
        {
            Name = "Existing",
            Priority = TicketPriority.High,
            ResponseTimeMinutes = 30,
            ResolutionTimeMinutes = 240
        };
        var toUpdate = new SlaPolicy
        {
            Name = "Other",
            Priority = TicketPriority.Low,
            ResponseTimeMinutes = 60,
            ResolutionTimeMinutes = 480,
            IsActive = false
        };
        dbContext.SlaPolicies.AddRange(existing, toUpdate);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateSlaPolicyCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpdateSlaPolicyCommand(toUpdate.Id, "Other", TicketPriority.High, 60, 480, true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
