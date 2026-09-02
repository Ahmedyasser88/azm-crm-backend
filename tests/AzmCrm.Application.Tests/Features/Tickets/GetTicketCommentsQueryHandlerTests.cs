using AzmCrm.Application.Features.Tickets.Queries.GetTicketComments;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Tickets;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class GetTicketCommentsQueryHandlerTests
{
    private static async Task<(TestApplicationDbContext dbContext, Ticket ticket)> SeedTicketAsync()
    {
        var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        var ticket = new Ticket
        {
            CustomerId = customer.Id, Title = "T", Category = TicketCategory.General, Priority = TicketPriority.Low
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();
        return (dbContext, ticket);
    }

    [Fact]
    public async Task Comments_return_ordered_oldest_first()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var now = DateTime.UtcNow;
        var newest = new TicketComment { TicketId = ticket.Id, Content = "Newest", CreatedOn = now };
        var oldest = new TicketComment { TicketId = ticket.Id, Content = "Oldest", CreatedOn = now.AddMinutes(-10) };
        var middle = new TicketComment { TicketId = ticket.Id, Content = "Middle", CreatedOn = now.AddMinutes(-5) };
        dbContext.TicketComments.AddRange(newest, oldest, middle);
        await dbContext.SaveChangesAsync();

        var handler = new GetTicketCommentsQueryHandler(dbContext, new StubIdentityQueryService());

        var result = await handler.Handle(new GetTicketCommentsQuery(ticket.Id), CancellationToken.None);

        var contents = result.Data!.Items.Select(c => c.Content).ToList();
        Assert.Equal(["Oldest", "Middle", "Newest"], contents);
    }

    [Fact]
    public async Task Comments_for_missing_ticket_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new GetTicketCommentsQueryHandler(dbContext, new StubIdentityQueryService());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new GetTicketCommentsQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Comment_author_name_is_resolved_via_identity_service()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        var authorId = Guid.NewGuid();
        dbContext.TicketComments.Add(new TicketComment { TicketId = ticket.Id, Content = "Hello", CreatedBy = authorId });
        await dbContext.SaveChangesAsync();

        var identityService = new StubIdentityQueryService();
        identityService.Users[authorId] = ("Alice Agent", "alice@azm.com.sa");

        var handler = new GetTicketCommentsQueryHandler(dbContext, identityService);

        var result = await handler.Handle(new GetTicketCommentsQuery(ticket.Id), CancellationToken.None);

        var comment = Assert.Single(result.Data!.Items);
        Assert.Equal("Alice Agent", comment.CreatedByName);
    }

    [Fact]
    public async Task Comment_author_name_is_null_when_identity_lookup_misses()
    {
        var (dbContext, ticket) = await SeedTicketAsync();
        await using var _ = dbContext;

        dbContext.TicketComments.Add(new TicketComment { TicketId = ticket.Id, Content = "Hello", CreatedBy = Guid.NewGuid() });
        await dbContext.SaveChangesAsync();

        var handler = new GetTicketCommentsQueryHandler(dbContext, new StubIdentityQueryService());

        var result = await handler.Handle(new GetTicketCommentsQuery(ticket.Id), CancellationToken.None);

        var comment = Assert.Single(result.Data!.Items);
        Assert.Null(comment.CreatedByName);
    }
}
