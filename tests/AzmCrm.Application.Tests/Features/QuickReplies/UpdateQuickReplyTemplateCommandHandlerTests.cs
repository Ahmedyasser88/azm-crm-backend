using AzmCrm.Application.Features.QuickReplies.Commands.UpdateQuickReplyTemplate;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Domain.Features.QuickReplies;
using Xunit;

namespace AzmCrm.Application.Tests.Features.QuickReplies;

public class UpdateQuickReplyTemplateCommandHandlerTests
{
    [Fact]
    public async Task Update_modifies_title_and_body()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var template = new QuickReplyTemplate { Title = "Old", Body = "Old body" };
        dbContext.QuickReplyTemplates.Add(template);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateQuickReplyTemplateCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpdateQuickReplyTemplateCommand(template.Id, "New", "New body"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", template.Title);
        Assert.Equal("New body", template.Body);
    }

    [Fact]
    public async Task Update_missing_template_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new UpdateQuickReplyTemplateCommandHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateQuickReplyTemplateCommand(Guid.NewGuid(), "Title", "Body"), CancellationToken.None));
    }
}
