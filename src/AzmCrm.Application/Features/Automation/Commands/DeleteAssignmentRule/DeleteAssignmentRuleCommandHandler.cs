using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Automation.Commands.DeleteAssignmentRule;

internal sealed class DeleteAssignmentRuleCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteAssignmentRuleCommand, Result>
{
    public async Task<Result> Handle(DeleteAssignmentRuleCommand request, CancellationToken ct)
    {
        var rule = await dbContext.AssignmentRules.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Assignment rule '{request.Id}' was not found.");

        rule.IsDeleted = true;
        rule.DeletedBy = currentUserService.UserId ?? Guid.Empty;
        rule.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
