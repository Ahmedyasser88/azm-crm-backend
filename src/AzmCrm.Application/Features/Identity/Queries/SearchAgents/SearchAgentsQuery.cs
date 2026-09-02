using AzmCrm.Application.Features.Identity.DTOs;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Identity.Queries.SearchAgents;

public sealed record SearchAgentsQuery(string? Search = null, int PageSize = 10)
    : IRequest<Result<List<AgentSummaryDto>>>;
