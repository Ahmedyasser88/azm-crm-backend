namespace AzmCrm.Application.Features.KnowledgeBase.DTOs;

public sealed record UpdateKnowledgeArticleStepRequest(int StepNumber, string Title, string Description);
