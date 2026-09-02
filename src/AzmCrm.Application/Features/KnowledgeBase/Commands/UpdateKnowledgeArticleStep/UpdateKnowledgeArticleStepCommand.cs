using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.UpdateKnowledgeArticleStep;

public sealed record UpdateKnowledgeArticleStepCommand(
    Guid KnowledgeArticleId, Guid StepId, int StepNumber, string Title, string Description) : IRequest<Result>;
