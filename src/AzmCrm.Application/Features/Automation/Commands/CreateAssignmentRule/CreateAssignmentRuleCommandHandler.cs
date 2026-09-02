using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Automation;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.CreateAssignmentRule;

internal sealed class CreateAssignmentRuleCommandHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<CreateAssignmentRuleCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateAssignmentRuleCommand request, CancellationToken ct)
    {
        var (fullName, _) = await identityQueryService.GetUserInfoAsync(request.AssignedToUserId, ct);
        if (fullName is null)
            throw new NotFoundException($"Agent '{request.AssignedToUserId}' was not found.");

        var rule = new AssignmentRule
        {
            Name = request.Name,
            Category = request.Category,
            Priority = request.Priority,
            AssignedToUserId = request.AssignedToUserId,
            EvaluationOrder = request.EvaluationOrder
        };

        dbContext.AssignmentRules.Add(rule);
        await dbContext.SaveChangesAsync(ct);

        return Result<Guid>.Success(rule.Id);
    }
}
