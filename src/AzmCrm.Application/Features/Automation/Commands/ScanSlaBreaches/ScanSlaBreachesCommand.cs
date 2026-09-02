using AzmCrm.Domain.Common;
using MediatR;

namespace AzmCrm.Application.Features.Automation.Commands.ScanSlaBreaches;

/// <summary>
/// Finds overdue, not-yet-escalated tickets and escalates each one whose matching active
/// <see cref="AzmCrm.Domain.Features.Automation.EscalationRule"/> grace period has elapsed.
/// Returns the number of tickets escalated. Invoked on a timer by
/// <c>SlaMonitoringBackgroundService</c> (Infrastructure), and directly by tests.
/// </summary>
public sealed record ScanSlaBreachesCommand : IRequest<Result<int>>;
