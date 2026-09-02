using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using AzmCrm.Domain.Features.Sla;
using AzmCrm.Domain.Features.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Application.Features.Automation.Commands.ScanSlaBreaches;

internal sealed class ScanSlaBreachesCommandHandler(
    IApplicationDbContext dbContext,
    IIdentityQueryService identityQueryService,
    IEmailSender emailSender,
    ILogger<ScanSlaBreachesCommandHandler> logger)
    : IRequestHandler<ScanSlaBreachesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ScanSlaBreachesCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var openTickets = await dbContext.Tickets
            .Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .Where(t => t.ResponseDueOn != null || t.ResolutionDueOn != null)
            .ToListAsync(ct);

        if (openTickets.Count == 0)
            return Result<int>.Success(0);

        var activeEscalationRules = await dbContext.EscalationRules
            .Where(r => r.IsActive)
            .ToListAsync(ct);

        var newNotifications = new List<SlaBreachNotification>();
        var escalatedCount = 0;

        foreach (var ticket in openTickets)
        {
            // Response breach: RespondedOn still null past ResponseDueOn. Alert-only — never
            // escalates — and fires at most once per ticket (guarded by the "already notified"
            // check below), unlike the resolution path which re-evaluates every tick until the
            // ticket is escalated.
            if (ticket.RespondedOn is null && ticket.ResponseDueOn is not null && now > ticket.ResponseDueOn
                && !await dbContext.SlaBreachNotifications.AnyAsync(
                    n => n.TicketId == ticket.Id && n.BreachType == SlaBreachType.ResponseOverdue, ct))
            {
                newNotifications.Add(new SlaBreachNotification
                {
                    TicketId = ticket.Id,
                    BreachType = SlaBreachType.ResponseOverdue,
                    NotifiedUserId = ticket.AssignedToUserId,
                    Message = $"Ticket '{ticket.Title}' has not been responded to and is past its response SLA."
                });
            }

            // Resolution breach: identical matching logic to Story 19's original scan, now also
            // recording a notification alongside every escalation it performs.
            if (!ticket.IsEscalated && ticket.ResolutionDueOn is not null)
            {
                var rule = activeEscalationRules.FirstOrDefault(r => r.Priority == ticket.Priority)
                           ?? activeEscalationRules.FirstOrDefault(r => r.Priority == null);

                if (rule is not null && now >= ticket.ResolutionDueOn.Value.AddMinutes(rule.OverdueMinutes))
                {
                    ticket.IsEscalated = true;
                    ticket.EscalatedOn = now;

                    dbContext.TicketHistories.Add(new TicketHistory
                    {
                        TicketId = ticket.Id,
                        EventType = TicketHistoryEventType.Escalated,
                        Description = $"Automatically escalated: resolution SLA breached (rule '{rule.Name}')."
                    });

                    escalatedCount++;

                    newNotifications.Add(new SlaBreachNotification
                    {
                        TicketId = ticket.Id,
                        BreachType = SlaBreachType.ResolutionOverdue,
                        NotifiedUserId = ticket.AssignedToUserId,
                        Message = $"Ticket '{ticket.Title}' was automatically escalated for missing its resolution SLA."
                    });
                }
            }
        }

        if (newNotifications.Count == 0)
            return Result<int>.Success(escalatedCount);

        var notifiedUserIds = newNotifications
            .Where(n => n.NotifiedUserId is not null)
            .Select(n => n.NotifiedUserId!.Value)
            .Distinct();
        var userInfo = await identityQueryService.GetUsersInfoAsync(notifiedUserIds, ct);

        foreach (var notification in newNotifications)
        {
            if (notification.NotifiedUserId is not null &&
                userInfo.TryGetValue(notification.NotifiedUserId.Value, out var info) &&
                info.Email is not null)
            {
                try
                {
                    await emailSender.SendAsync(info.Email, "SLA breach alert", notification.Message, ct);
                    notification.EmailSent = true;
                }
                catch (Exception ex)
                {
                    // A failed email must not lose the notification row or fail the whole scan —
                    // the breach is still visible via GET /api/sla-breach-notifications either way.
                    logger.LogError(ex, "Failed to send SLA breach email for ticket {TicketId}.", notification.TicketId);
                }
            }

            dbContext.SlaBreachNotifications.Add(notification);
        }

        await dbContext.SaveChangesAsync(ct);

        return Result<int>.Success(escalatedCount);
    }
}
