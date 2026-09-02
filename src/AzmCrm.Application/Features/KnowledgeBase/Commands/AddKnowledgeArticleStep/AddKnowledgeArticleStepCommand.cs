using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.AddKnowledgeArticleStep;

public sealed record AddKnowledgeArticleStepCommand(
    Guid KnowledgeArticleId, int StepNumber, string Title, string Description) : IRequest<Result<Guid>>;
