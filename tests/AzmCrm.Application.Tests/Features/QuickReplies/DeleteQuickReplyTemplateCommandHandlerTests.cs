using AzmCrm.Application.Features.QuickReplies.Commands.DeleteQuickReplyTemplate;
using AzmCrm.Application.Features.QuickReplies.Queries.GetQuickReplyTemplateById;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.QuickReplies;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AzmCrm.Application.Tests.Features.QuickReplies;

public class DeleteQuickReplyTemplateCommandHandlerTests
{
    [Fact]
    public async Task Delete_sets_IsDeleted_and_DeletedBy_DeletedOn()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var template = new QuickReplyTemplate { Title = "T", Body = "B" };
        dbContext.QuickReplyTemplates.Add(template);
        await dbContext.SaveChangesAsync();

        var currentUser = new StubCurrentUserService();
        var handler = new DeleteQuickReplyTemplateCommandHandler(dbContext, currentUser);

        var result = await handler.Handle(new DeleteQuickReplyTemplateCommand(template.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var persisted = await dbContext.QuickReplyTemplates.IgnoreQueryFilters()
            .SingleAsync(t => t.Id == template.Id);
        Assert.True(persisted.IsDeleted);
        Assert.Equal(currentUser.UserId, persisted.DeletedBy);
        Assert.NotNull(persisted.DeletedOn);
    }

    [Fact]
    public async Task Delete_missing_template_throws_NotFoundException()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var handler = new DeleteQuickReplyTemplateCommandHandler(dbContext, new StubCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new DeleteQuickReplyTemplateCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Deleted_template_is_excluded_from_GetById()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var template = new QuickReplyTemplate { Title = "T", Body = "B" };
        dbContext.QuickReplyTemplates.Add(template);
        await dbContext.SaveChangesAsync();

        var deleteHandler = new DeleteQuickReplyTemplateCommandHandler(dbContext, new StubCurrentUserService());
        await deleteHandler.Handle(new DeleteQuickReplyTemplateCommand(template.Id), CancellationToken.None);

        var getByIdHandler = new GetQuickReplyTemplateByIdQueryHandler(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(
            () => getByIdHandler.Handle(new GetQuickReplyTemplateByIdQuery(template.Id), CancellationToken.None));
    }
}
