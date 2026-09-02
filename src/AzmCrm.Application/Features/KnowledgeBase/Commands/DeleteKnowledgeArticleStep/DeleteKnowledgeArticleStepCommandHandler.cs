using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticleStep;

internal sealed class DeleteKnowledgeArticleStepCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteKnowledgeArticleStepCommand, Result>
{
    public async Task<Result> Handle(DeleteKnowledgeArticleStepCommand request, CancellationToken ct)
    {
        var step = await dbContext.KnowledgeArticleSteps
            .FirstOrDefaultAsync(s => s.Id == request.StepId && s.KnowledgeArticleId == request.KnowledgeArticleId, ct)
            ?? throw new NotFoundException($"Step '{request.StepId}' was not found.");

        step.IsDeleted = true;
        step.DeletedBy = currentUserService.UserId ?? Guid.Empty;
        step.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
