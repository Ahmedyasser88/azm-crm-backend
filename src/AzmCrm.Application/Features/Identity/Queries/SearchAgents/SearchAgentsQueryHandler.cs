using AzmCrm.Application.Features.Identity.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Identity.Queries.SearchAgents;

internal sealed class SearchAgentsQueryHandler(IIdentityQueryService identityQueryService)
    : IRequestHandler<SearchAgentsQuery, Result<List<AgentSummaryDto>>>
{
    public async Task<Result<List<AgentSummaryDto>>> Handle(SearchAgentsQuery request, CancellationToken ct)
    {
        var agents = await identityQueryService.SearchAgentsAsync(request.Search, request.PageSize, ct);

        var dtos = agents.Select(a => new AgentSummaryDto(a.Id, a.FullName, a.Email)).ToList();

        return Result<List<AgentSummaryDto>>.Success(dtos);
    }
}
