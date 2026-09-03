using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Commands.GenerateTicketSummary;

internal sealed class GenerateTicketSummaryCommandHandler(IApplicationDbContext dbContext, IAiClient aiClient)
    : IRequestHandler<GenerateTicketSummaryCommand, Result<TicketAiSummaryDto>>
{
    private const int MaxCommentsInContext = 20;

    public async Task<Result<TicketAiSummaryDto>> Handle(GenerateTicketSummaryCommand request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        // Bounded to the most recent MaxCommentsInContext comments so the prompt does not grow
        // unbounded on a long-running ticket — an explicit scope choice, not an oversight.
        var comments = await dbContext.TicketComments
            .Where(c => c.TicketId == ticket.Id)
            .OrderByDescending(c => c.CreatedOn)
            .Take(MaxCommentsInContext)
            .OrderBy(c => c.CreatedOn)
            .Select(c => c.Content)
            .ToListAsync(ct);

        var userPrompt =
            $"Title: {ticket.Title}\n" +
            $"Category: {ticket.Category}\n" +
            $"Priority: {ticket.Priority}\n" +
            $"Status: {ticket.Status}\n" +
            $"Description: {ticket.Description ?? "(none)"}\n\n" +
            (comments.Count > 0
                ? "Internal comment thread (oldest first):\n" + string.Join("\n---\n", comments)
                : "No internal comments yet.");

        const string systemPrompt =
            "You are an assistant that writes concise internal summaries of customer support tickets " +
            "for a support agent. Summarize the ticket in 2-3 sentences: the customer's issue, its " +
            "current state, and any progress so far. Do not invent facts not present in the ticket.";

        string summary;
        try
        {
            summary = await aiClient.GetCompletionAsync(systemPrompt, userPrompt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TicketAiSummaryDto>.Failure("AI summary generation is currently unavailable. Please try again later.");
        }

        ticket.AiSummary = summary;
        ticket.AiSummaryGeneratedOn = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        return Result<TicketAiSummaryDto>.Success(
            new TicketAiSummaryDto(ticket.Id, ticket.AiSummary, ticket.AiSummaryGeneratedOn.Value));
    }
}
