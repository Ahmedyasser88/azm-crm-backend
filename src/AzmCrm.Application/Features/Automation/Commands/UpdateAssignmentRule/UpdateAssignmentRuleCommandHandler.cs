using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Automation.Commands.UpdateAssignmentRule;

internal sealed class UpdateAssignmentRuleCommandHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService)
    : IRequestHandler<UpdateAssignmentRuleCommand, Result>
{
    public async Task<Result> Handle(UpdateAssignmentRuleCommand request, CancellationToken ct)
    {
        var rule = await dbContext.AssignmentRules.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Assignment rule '{request.Id}' was not found.");

        var (fullName, _) = await identityQueryService.GetUserInfoAsync(request.AssignedToUserId, ct);
        if (fullName is null)
            throw new NotFoundException($"Agent '{request.AssignedToUserId}' was not found.");

        rule.Name = request.Name;
        rule.Category = request.Category;
        rule.Priority = request.Priority;
        rule.AssignedToUserId = request.AssignedToUserId;
        rule.EvaluationOrder = request.EvaluationOrder;
        rule.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
