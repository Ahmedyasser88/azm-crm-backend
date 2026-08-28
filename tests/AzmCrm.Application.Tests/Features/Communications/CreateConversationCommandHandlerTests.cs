using AzmCrm.Application.Features.Communications.Commands.CreateConversation;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.Communications;
using AzmCrm.Domain.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class CreateConversationCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_conversation_with_Open_status()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var customer = new Customer { FullName = "Jane Doe" };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        var handler = new CreateConversationCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateConversationCommand(customer.Id, CommunicationChannel.Email, "Order question"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var conversation = await dbContext.Conversations.SingleAsync(c => c.Id == result.Data);
        Assert.Equal(ConversationStatus.Open, conversation.Status);
        Assert.Equal(CommunicationChannel.Email, conversation.Channel);
    }

    [Fact]
    public async Task Create_for_missing_customer_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateConversationCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(
                new CreateConversationCommand(Guid.NewGuid(), CommunicationChannel.Email, null),
                CancellationToken.None));
    }
}
