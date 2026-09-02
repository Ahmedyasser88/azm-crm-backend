using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Automation.Commands.UpdateEscalationRule;

internal sealed class UpdateEscalationRuleCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateEscalationRuleCommand, Result>
{
    public async Task<Result> Handle(UpdateEscalationRuleCommand request, CancellationToken ct)
    {
        var rule = await dbContext.EscalationRules.FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Escalation rule '{request.Id}' was not found.");

        rule.Name = request.Name;
        rule.Priority = request.Priority;
        rule.OverdueMinutes = request.OverdueMinutes;
        rule.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}
