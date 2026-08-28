using AzmCrm.Application.Features.Communications.Queries.GetConversationsList;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class GetConversationsListQueryHandlerTests
{
    private static async Task<(TestApplicationDbContext DbContext, Customer Customer)> SeedCustomerAsync()
    {
        var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();
        return (dbContext, customer);
    }

    [Fact]
    public async Task List_returns_paginated_results_ordered_by_CreatedOn_desc()
    {
        var (dbContext, customer) = await SeedCustomerAsync();
        await using var _ = dbContext;

        var older = new Conversation { CustomerId = customer.Id, Channel = CommunicationChannel.Email };
        dbContext.Conversations.Add(older);
        await dbContext.SaveChangesAsync();

        var newer = new Conversation { CustomerId = customer.Id, Channel = CommunicationChannel.Sms };
        dbContext.Conversations.Add(newer);
        await dbContext.SaveChangesAsync();

        var handler = new GetConversationsListQueryHandler(dbContext);
        var result = await handler.Handle(new GetConversationsListQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var items = result.Data!.Items.ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(newer.Id, items[0].Id);
        Assert.Equal(older.Id, items[1].Id);
    }

    [Fact]
    public async Task List_filters_by_customerId_channel_and_status()
    {
        var (dbContext, customer) = await SeedCustomerAsync();
        await using var _ = dbContext;

        var otherCustomer = new Customer { FullName = "John Roe" };
        dbContext.Customers.Add(otherCustomer);

        var emailConversation = new Conversation { CustomerId = customer.Id, Channel = CommunicationChannel.Email };
        var smsConversation = new Conversation { CustomerId = customer.Id, Channel = CommunicationChannel.Sms };
        var otherCustomerConversation = new Conversation
        {
            CustomerId = otherCustomer.Id,
            Channel = CommunicationChannel.Email
        };
        dbContext.Conversations.AddRange(emailConversation, smsConversation, otherCustomerConversation);
        await dbContext.SaveChangesAsync();

        var handler = new GetConversationsListQueryHandler(dbContext);

        var byCustomer = await handler.Handle(
            new GetConversationsListQuery(CustomerId: customer.Id), CancellationToken.None);
        Assert.Equal(2, byCustomer.Data!.Items.Count());

        var byChannel = await handler.Handle(
            new GetConversationsListQuery(Channel: CommunicationChannel.Sms), CancellationToken.None);
        Assert.Equal(smsConversation.Id, Assert.Single(byChannel.Data!.Items).Id);

        var byStatus = await handler.Handle(
            new GetConversationsListQuery(Status: ConversationStatus.Open), CancellationToken.None);
        Assert.Equal(3, byStatus.Data!.Items.Count());
    }
}
