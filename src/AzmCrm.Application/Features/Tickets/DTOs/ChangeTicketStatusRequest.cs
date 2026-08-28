using AzmCrm.Domain.Features.Tickets;

namespace AzmCrm.Application.Features.Tickets.DTOs;

public sealed record ChangeTicketStatusRequest(TicketStatus Status);
