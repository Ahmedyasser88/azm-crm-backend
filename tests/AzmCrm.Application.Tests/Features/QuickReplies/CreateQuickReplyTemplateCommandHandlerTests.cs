using AzmCrm.Application.Features.QuickReplies.Commands.CreateQuickReplyTemplate;
using Xunit;

namespace AzmCrm.Application.Tests.Features.QuickReplies;

public class CreateQuickReplyTemplateCommandHandlerTests
{
    [Fact]
    public async Task Create_persists_template()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new CreateQuickReplyTemplateCommandHandler(dbContext);

        var result = await handler.Handle(
            new CreateQuickReplyTemplateCommand("Order Delay", "We're sorry for the delay."),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var template = Assert.Single(dbContext.QuickReplyTemplates);
        Assert.Equal("Order Delay", template.Title);
        Assert.Equal("We're sorry for the delay.", template.Body);
    }
}
