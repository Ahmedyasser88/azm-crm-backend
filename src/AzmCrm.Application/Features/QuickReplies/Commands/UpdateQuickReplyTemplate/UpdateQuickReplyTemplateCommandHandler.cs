using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.QuickReplies.Commands.UpdateQuickReplyTemplate;

internal sealed class UpdateQuickReplyTemplateCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateQuickReplyTemplateCommand, Result>
{
    public async Task<Result> Handle(UpdateQuickReplyTemplateCommand request, CancellationToken ct)
    {
        var template = await dbContext.QuickReplyTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException($"Quick reply template '{request.Id}' was not found.");

        template.Title = request.Title;
        template.Body = request.Body;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
