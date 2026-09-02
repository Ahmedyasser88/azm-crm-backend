using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.KnowledgeBase.Commands.DeleteKnowledgeArticleStep;

public sealed record DeleteKnowledgeArticleStepCommand(Guid KnowledgeArticleId, Guid StepId) : IRequest<Result>;
