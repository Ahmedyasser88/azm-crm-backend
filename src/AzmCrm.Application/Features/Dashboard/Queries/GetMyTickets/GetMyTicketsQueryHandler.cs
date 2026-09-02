using AzmCrm.Application.Features.Dashboard.DTOs;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Application.Shared.Models;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Dashboard.Queries.GetMyTickets;

internal sealed class GetMyTicketsQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetMyTicketsQuery, Result<PaginatedResult<DashboardTicketDto>>>
{
    public async Task<Result<PaginatedResult<DashboardTicketDto>>> Handle(
        GetMyTicketsQuery request, CancellationToken ct)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;

        var query = dbContext.Tickets.Where(t => t.AssignedToUserId == userId);

        if (request.Status is not null)
            query = query.Where(t => t.Status == request.Status);

        var totalCount = await query.CountAsync(ct);

        var tickets = await query
            .OrderByDescending(t => t.CreatedOn)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var customerIds = tickets.Select(t => t.CustomerId).Distinct();
        var customers = await dbContext.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var items = tickets.Select(t => new DashboardTicketDto(
            t.Id, t.Title, t.Category, t.Priority, t.Status, t.CreatedOn, t.IsEscalated, t.EscalatedOn,
            t.ResolutionDueOn,
            customers.TryGetValue(t.CustomerId, out var customer)
                ? new CustomerSummaryDto(customer.Id, customer.FullName, customer.CompanyName, customer.Email, customer.PhoneNumber)
                : null));

        var result = new PaginatedResult<DashboardTicketDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        return Result<PaginatedResult<DashboardTicketDto>>.Success(result);
    }
}
