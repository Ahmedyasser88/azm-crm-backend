using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Classifies a new ticket's title/description into one of the existing <see cref="TicketCategory"/>
/// values when no category was supplied at creation time. Implementations must never throw — an
/// unavailable AI provider or an unparseable response must fall back to
/// <see cref="TicketCategory.General"/> rather than blocking ticket creation.
/// </summary>
public interface IIncomingTicketCategorizer
{
    Task<TicketCategory> CategorizeAsync(string title, string? description, CancellationToken ct = default);
}
