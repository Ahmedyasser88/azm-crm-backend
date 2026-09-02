using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.AddKnowledgeArticleStep;

internal sealed class AddKnowledgeArticleStepCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<AddKnowledgeArticleStepCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddKnowledgeArticleStepCommand request, CancellationToken ct)
    {
        var articleExists = await dbContext.KnowledgeArticles.AnyAsync(a => a.Id == request.KnowledgeArticleId, ct);
        if (!articleExists)
            throw new NotFoundException($"Knowledge article '{request.KnowledgeArticleId}' was not found.");

        var step = new KnowledgeArticleStep
        {
            KnowledgeArticleId = request.KnowledgeArticleId,
            StepNumber = request.StepNumber,
            Title = request.Title,
            Description = request.Description
        };

        dbContext.KnowledgeArticleSteps.Add(step);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(step.Id);
    }
}
