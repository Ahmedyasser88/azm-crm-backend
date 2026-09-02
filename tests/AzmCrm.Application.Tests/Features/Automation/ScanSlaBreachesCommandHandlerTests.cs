using AzmCrm.Application.Features.Automation.Commands.ScanSlaBreaches;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.Automation;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Automation;

public class ScanSlaBreachesCommandHandlerTests
{
    private static ScanSlaBreachesCommandHandler CreateHandler(
        TestApplicationDbContext dbContext, StubIdentityQueryService? identity = null, StubEmailSender? email = null) =>
        new(dbContext, identity ?? new StubIdentityQueryService(), email ?? new StubEmailSender(),
            NullLogger<ScanSlaBreachesCommandHandler>.Instance);

    private static async Task<(TestApplicationDbContext DbContext, Ticket Ticket)> SeedOverdueTicketAsync(
        TicketPriority priority = TicketPriority.High, TicketStatus status = TicketStatus.Open,
        bool isEscalated = false, DateTime? resolutionDueOn = null, Guid? assignedToUserId = null)
    {
        var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "Overdue ticket",
            Category = TicketCategory.General,
            Priority = priority,
            Status = status,
            IsEscalated = isEscalated,
            ResolutionDueOn = resolutionDueOn ?? DateTime.UtcNow.AddHours(-1),
            AssignedToUserId = assignedToUserId
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        return (dbContext, ticket);
    }

    [Fact]
    public async Task Scan_escalates_ticket_past_its_grace_period()
    {
        var (dbContext, ticket) = await SeedOverdueTicketAsync();
        await using var _ = dbContext;

        dbContext.EscalationRules.Add(new EscalationRule { Name = "High", Priority = TicketPriority.High, OverdueMinutes = 0 });
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var result = await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data);

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.True(persisted.IsEscalated);
        Assert.NotNull(persisted.EscalatedOn);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        var entry = Assert.Single(history);
        Assert.Equal(TicketHistoryEventType.Escalated, entry.EventType);
    }

    [Fact]
    public async Task Scan_does_not_escalate_ticket_still_within_grace_period()
    {
        var (dbContext, ticket) = await SeedOverdueTicketAsync(resolutionDueOn: DateTime.UtcNow.AddMinutes(-5));
        await using var _ = dbContext;

        dbContext.EscalationRules.Add(new EscalationRule { Name = "High", Priority = TicketPriority.High, OverdueMinutes = 60 });
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var result = await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        Assert.Equal(0, result.Data);

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.False(persisted.IsEscalated);
    }

    [Fact]
    public async Task Scan_does_not_escalate_already_escalated_ticket()
    {
        var (dbContext, _) = await SeedOverdueTicketAsync(isEscalated: true);
        await using var _ = dbContext;

        dbContext.EscalationRules.Add(new EscalationRule { Name = "High", Priority = TicketPriority.High, OverdueMinutes = 0 });
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var result = await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        Assert.Equal(0, result.Data);
    }

    [Fact]
    public async Task Scan_does_not_escalate_ticket_with_null_ResolutionDueOn()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);
        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "No SLA",
            Category = TicketCategory.General,
            Priority = TicketPriority.High
        };
        dbContext.Tickets.Add(ticket);
        dbContext.EscalationRules.Add(new EscalationRule { Name = "High", Priority = TicketPriority.High, OverdueMinutes = 0 });
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var result = await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        Assert.Equal(0, result.Data);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task Scan_does_not_escalate_Resolved_or_Closed_ticket(TicketStatus status)
    {
        var (dbContext, _) = await SeedOverdueTicketAsync(status: status);
        await using var _ = dbContext;

        dbContext.EscalationRules.Add(new EscalationRule { Name = "High", Priority = TicketPriority.High, OverdueMinutes = 0 });
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var result = await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        Assert.Equal(0, result.Data);
    }

    [Fact]
    public async Task Scan_prefers_priority_specific_rule_over_catchall_rule()
    {
        var (dbContext, ticket) = await SeedOverdueTicketAsync(resolutionDueOn: DateTime.UtcNow.AddMinutes(-10));
        await using var _ = dbContext;

        dbContext.EscalationRules.AddRange(
            new EscalationRule { Name = "High specific", Priority = TicketPriority.High, OverdueMinutes = 0 },
            new EscalationRule { Name = "Catch-all", Priority = null, OverdueMinutes = 1000 });
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var result = await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        Assert.Equal(1, result.Data);

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.True(persisted.IsEscalated);
    }

    [Fact]
    public async Task Scan_skips_ticket_with_no_matching_rule_and_no_catchall()
    {
        var (dbContext, ticket) = await SeedOverdueTicketAsync(priority: TicketPriority.Low);
        await using var _ = dbContext;

        dbContext.EscalationRules.Add(new EscalationRule { Name = "High only", Priority = TicketPriority.High, OverdueMinutes = 0 });
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var result = await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        Assert.Equal(0, result.Data);

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.False(persisted.IsEscalated);
    }

    [Fact]
    public async Task Scan_with_no_overdue_tickets_returns_zero_without_touching_EscalationRules()
    {
        await using var dbContext = TestApplicationDbContext.Create();

        var handler = CreateHandler(dbContext);
        var result = await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data);
    }

    [Fact]
    public async Task Scan_creates_ResponseOverdue_notification_and_sends_email()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);

        var agentId = Guid.NewGuid();
        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "Unanswered",
            Category = TicketCategory.General,
            Priority = TicketPriority.High,
            ResponseDueOn = DateTime.UtcNow.AddMinutes(-10),
            AssignedToUserId = agentId
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var identity = new StubIdentityQueryService();
        identity.Users[agentId] = ("Agent Smith", "agent@azm.com");
        var email = new StubEmailSender();

        var handler = CreateHandler(dbContext, identity, email);
        await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        var notification = await dbContext.SlaBreachNotifications.SingleAsync(n => n.TicketId == ticket.Id);
        Assert.Equal(SlaBreachType.ResponseOverdue, notification.BreachType);
        Assert.True(notification.EmailSent);
        Assert.Single(email.SentEmails);
    }

    [Fact]
    public async Task Scan_does_not_duplicate_ResponseOverdue_notification_on_second_tick()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);

        var agentId = Guid.NewGuid();
        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Title = "Unanswered",
            Category = TicketCategory.General,
            Priority = TicketPriority.High,
            ResponseDueOn = DateTime.UtcNow.AddMinutes(-10),
            AssignedToUserId = agentId
        };
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var identity = new StubIdentityQueryService();
        identity.Users[agentId] = ("Agent Smith", "agent@azm.com");
        var email = new StubEmailSender();

        var handler = CreateHandler(dbContext, identity, email);
        await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);
        await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        var count = await dbContext.SlaBreachNotifications.CountAsync(n => n.TicketId == ticket.Id);
        Assert.Equal(1, count);
        Assert.Single(email.SentEmails);
    }

    [Fact]
    public async Task Scan_creates_ResolutionOverdue_notification_alongside_escalation()
    {
        var agentId = Guid.NewGuid();
        var (dbContext, ticket) = await SeedOverdueTicketAsync(assignedToUserId: agentId);
        await using var _ = dbContext;

        dbContext.EscalationRules.Add(new EscalationRule { Name = "High", Priority = TicketPriority.High, OverdueMinutes = 0 });
        await dbContext.SaveChangesAsync();

        var identity = new StubIdentityQueryService();
        identity.Users[agentId] = ("Agent Smith", "agent@azm.com");
        var email = new StubEmailSender();

        var handler = CreateHandler(dbContext, identity, email);
        var result = await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        Assert.Equal(1, result.Data);

        var persisted = await dbContext.Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.True(persisted.IsEscalated);

        var notification = await dbContext.SlaBreachNotifications.SingleAsync(n => n.TicketId == ticket.Id);
        Assert.Equal(SlaBreachType.ResolutionOverdue, notification.BreachType);
        Assert.True(notification.EmailSent);
    }

    [Fact]
    public async Task Scan_unassigned_ticket_creates_notification_with_null_NotifiedUserId_and_no_email()
    {
        var (dbContext, ticket) = await SeedOverdueTicketAsync();
        await using var _ = dbContext;

        dbContext.EscalationRules.Add(new EscalationRule { Name = "High", Priority = TicketPriority.High, OverdueMinutes = 0 });
        await dbContext.SaveChangesAsync();

        var email = new StubEmailSender();
        var handler = CreateHandler(dbContext, email: email);
        await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        var notification = await dbContext.SlaBreachNotifications.SingleAsync(n => n.TicketId == ticket.Id);
        Assert.Null(notification.NotifiedUserId);
        Assert.False(notification.EmailSent);
        Assert.Empty(email.SentEmails);
    }

    [Fact]
    public async Task Scan_email_failure_does_not_prevent_notification_persistence()
    {
        var agentId = Guid.NewGuid();
        var (dbContext, ticket) = await SeedOverdueTicketAsync(assignedToUserId: agentId);
        await using var _ = dbContext;

        dbContext.EscalationRules.Add(new EscalationRule { Name = "High", Priority = TicketPriority.High, OverdueMinutes = 0 });
        await dbContext.SaveChangesAsync();

        var identity = new StubIdentityQueryService();
        identity.Users[agentId] = ("Agent Smith", "agent@azm.com");
        var email = new StubEmailSender { ThrowOnSend = true };

        var handler = CreateHandler(dbContext, identity, email);
        var result = await handler.Handle(new ScanSlaBreachesCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var notification = await dbContext.SlaBreachNotifications.SingleAsync(n => n.TicketId == ticket.Id);
        Assert.False(notification.EmailSent);
        Assert.Empty(email.SentEmails);
    }
}
