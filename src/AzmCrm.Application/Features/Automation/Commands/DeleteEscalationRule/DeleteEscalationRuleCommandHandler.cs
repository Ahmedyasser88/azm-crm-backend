using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Automation.Commands.DeleteEscalationRule;

internal sealed class DeleteEscalationRuleCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteEscalationRuleCommand, Result>
{
    public async Task<Result> Handle(DeleteEscalationRuleCommand request, CancellationToken ct)
    {
        var rule = await dbContext.EscalationRules.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Escalation rule '{request.Id}' was not found.");

        rule.IsDeleted = true;
        rule.DeletedBy = currentUserService.UserId ?? Guid.Empty;
        rule.DeletedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
