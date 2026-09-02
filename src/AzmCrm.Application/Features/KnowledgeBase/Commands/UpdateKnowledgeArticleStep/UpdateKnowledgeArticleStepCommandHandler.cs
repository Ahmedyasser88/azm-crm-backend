using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticleStep;

internal sealed class UpdateKnowledgeArticleStepCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateKnowledgeArticleStepCommand, Result>
{
    public async Task<Result> Handle(UpdateKnowledgeArticleStepCommand request, CancellationToken ct)
    {
        var step = await dbContext.KnowledgeArticleSteps
            .FirstOrDefaultAsync(s => s.Id == request.StepId && s.KnowledgeArticleId == request.KnowledgeArticleId, ct)
            ?? throw new NotFoundException($"Step '{request.StepId}' was not found.");

        step.StepNumber = request.StepNumber;
        step.Title = request.Title;
        step.Description = request.Description;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
