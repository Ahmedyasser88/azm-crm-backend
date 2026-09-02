using AzmCrm.Application.Features.Sla.Commands.DeleteSlaPolicy;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Sla;

public class DeleteSlaPolicyCommandHandlerTests
{
    [Fact]
    public async Task Delete_soft_deletes_policy()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var policy = new SlaPolicy
        {
            Name = "P",
            Priority = TicketPriority.Low,
            ResponseTimeMinutes = 60,
            ResolutionTimeMinutes = 480
        };
        dbContext.SlaPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        var handler = new DeleteSlaPolicyCommandHandler(dbContext, new StubCurrentUserService());

        var result = await handler.Handle(new DeleteSlaPolicyCommand(policy.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.SlaPolicies.IgnoreQueryFilters().SingleAsync(p => p.Id == policy.Id);
        Assert.True(persisted.IsDeleted);
    }

    [Fact]
    public async Task Delete_missing_policy_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new DeleteSlaPolicyCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new DeleteSlaPolicyCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
