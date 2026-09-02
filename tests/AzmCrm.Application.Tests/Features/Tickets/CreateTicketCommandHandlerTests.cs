using AzmCrm.Application.Features.Tickets.Commands.CreateTicket;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Automation;
using AzmCrm.Domain.Features.Customers;
using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Tickets;

public class CreateTicketCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_ticket_with_New_status_and_logs_Created_history()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var handler = new CreateTicketCommandHandler(dbContext);

        var command = new CreateTicketCommand(
            customer.Id, "Cannot log in", "User gets 401 on login", TicketCategory.Technical, TicketPriority.High);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var ticket = await dbContext.Tickets.SingleAsync(t => t.Id == result.Data);
        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal(customer.Id, ticket.CustomerId);
        Assert.Equal("Cannot log in", ticket.Title);
        Assert.Equal(TicketCategory.Technical, ticket.Category);
        Assert.Equal(TicketPriority.High, ticket.Priority);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        var entry = Assert.Single(history);
        Assert.Equal(TicketHistoryEventType.Created, entry.EventType);
    }

    [Fact]
    public async Task Create_for_missing_customer_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateTicketCommandHandler(dbContext);

        var command = new CreateTicketCommand(
            Guid.NewGuid(), "Cannot log in", null, TicketCategory.Technical, TicketPriority.High);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Create_with_matching_active_SlaPolicy_stamps_due_dates()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);

        var policy = new SlaPolicy
        {
            Name = "High priority",
            Priority = TicketPriority.High,
            ResponseTimeMinutes = 30,
            ResolutionTimeMinutes = 240
        };
        dbContext.SlaPolicies.Add(policy);
        await dbContext.SaveChangesAsync();

        var handler = new CreateTicketCommandHandler(dbContext);

        var command = new CreateTicketCommand(
            customer.Id, "Cannot log in", null, TicketCategory.Technical, TicketPriority.High);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var ticket = await dbContext.Tickets.SingleAsync(t => t.Id == result.Data);
        Assert.Equal(policy.Id, ticket.SlaPolicyId);
        Assert.Equal(ticket.CreatedOn.AddMinutes(30), ticket.ResponseDueOn);
        Assert.Equal(ticket.CreatedOn.AddMinutes(240), ticket.ResolutionDueOn);
    }

    [Fact]
    public async Task Create_with_no_matching_SlaPolicy_leaves_due_dates_null()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var handler = new CreateTicketCommandHandler(dbContext);

        var command = new CreateTicketCommand(
            customer.Id, "Cannot log in", null, TicketCategory.Technical, TicketPriority.Low);

        var result = await handler.Handle(command, CancellationToken.None);

        var ticket = await dbContext.Tickets.SingleAsync(t => t.Id == result.Data);
        Assert.Null(ticket.SlaPolicyId);
        Assert.Null(ticket.ResponseDueOn);
        Assert.Null(ticket.ResolutionDueOn);
    }

    [Fact]
    public async Task Create_matching_active_rule_auto_assigns_and_logs_history()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);

        var agentId = Guid.NewGuid();
        var rule = new AssignmentRule
        {
            Name = "Billing rule",
            Category = TicketCategory.Billing,
            AssignedToUserId = agentId,
            EvaluationOrder = 1
        };
        dbContext.AssignmentRules.Add(rule);
        await dbContext.SaveChangesAsync();

        var handler = new CreateTicketCommandHandler(dbContext);

        var command = new CreateTicketCommand(
            customer.Id, "Invoice question", null, TicketCategory.Billing, TicketPriority.Low);

        var result = await handler.Handle(command, CancellationToken.None);

        var ticket = await dbContext.Tickets.SingleAsync(t => t.Id == result.Data);
        Assert.Equal(agentId, ticket.AssignedToUserId);

        var history = await dbContext.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.EventType == TicketHistoryEventType.Assigned);
    }

    [Fact]
    public async Task Create_with_no_matching_rule_leaves_ticket_unassigned()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);
        dbContext.AssignmentRules.Add(new AssignmentRule
        {
            Name = "Billing rule",
            Category = TicketCategory.Billing,
            AssignedToUserId = Guid.NewGuid(),
            EvaluationOrder = 1
        });
        await dbContext.SaveChangesAsync();

        var handler = new CreateTicketCommandHandler(dbContext);

        var command = new CreateTicketCommand(
            customer.Id, "Login issue", null, TicketCategory.Technical, TicketPriority.Low);

        var result = await handler.Handle(command, CancellationToken.None);

        var ticket = await dbContext.Tickets.SingleAsync(t => t.Id == result.Data);
        Assert.Null(ticket.AssignedToUserId);
    }

    [Fact]
    public async Task Create_picks_lowest_EvaluationOrder_among_multiple_matches()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Acme Corp" };
        dbContext.Customers.Add(customer);

        var lowOrderAgent = Guid.NewGuid();
        var highOrderAgent = Guid.NewGuid();
        dbContext.AssignmentRules.AddRange(
            new AssignmentRule { Name = "High order", AssignedToUserId = highOrderAgent, EvaluationOrder = 5 },
            new AssignmentRule { Name = "Low order", AssignedToUserId = lowOrderAgent, EvaluationOrder = 1 });
        await dbContext.SaveChangesAsync();

        var handler = new CreateTicketCommandHandler(dbContext);

        var command = new CreateTicketCommand(
            customer.Id, "Generic", null, TicketCategory.General, TicketPriority.Low);

        var result = await handler.Handle(command, CancellationToken.None);

        var ticket = await dbContext.Tickets.SingleAsync(t => t.Id == result.Data);
        Assert.Equal(lowOrderAgent, ticket.AssignedToUserId);
    }
}
