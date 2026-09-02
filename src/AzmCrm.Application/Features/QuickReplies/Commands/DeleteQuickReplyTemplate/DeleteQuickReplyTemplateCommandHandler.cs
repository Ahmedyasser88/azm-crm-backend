using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.QuickReplies.Commands.DeleteQuickReplyTemplate;

internal sealed class DeleteQuickReplyTemplateCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteQuickReplyTemplateCommand, Result>
{
    public async Task<Result> Handle(DeleteQuickReplyTemplateCommand request, CancellationToken ct)
    {
        var template = await dbContext.QuickReplyTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException($"Quick reply template '{request.Id}' was not found.");

        template.IsDeleted = true;
        template.DeletedBy = currentUserService.UserId ?? Guid.Empty;
        template.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
