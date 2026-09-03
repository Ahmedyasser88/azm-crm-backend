using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Tests.TestDoubles;

public sealed class StubIncomingTicketCategorizer : IIncomingTicketCategorizer
{
    public List<(string Title, string? Description)> Calls { get; } = [];
    public TicketCategory CategoryToReturn { get; set; } = TicketCategory.General;

    public Task<TicketCategory> CategorizeAsync(string title, string? description, CancellationToken ct = default)
    {
        Calls.Add((title, description));
        return Task.FromResult(CategoryToReturn);
    }
}
