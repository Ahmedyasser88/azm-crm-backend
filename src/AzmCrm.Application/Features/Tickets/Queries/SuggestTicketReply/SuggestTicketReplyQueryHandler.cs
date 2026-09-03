using AzmCrm.Application.Features.Tickets.DTOs;
using AzmCrm.Application.Shared.Exceptions;
using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Tickets.Queries.SuggestTicketReply;

internal sealed class SuggestTicketReplyQueryHandler(IApplicationDbContext dbContext, IAiClient aiClient)
    : IRequestHandler<SuggestTicketReplyQuery, Result<TicketReplySuggestionDto>>
{
    private const int MaxCommentsInContext = 20;

    public async Task<Result<TicketReplySuggestionDto>> Handle(SuggestTicketReplyQuery request, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == request.TicketId, ct)
            ?? throw new NotFoundException($"Ticket '{request.TicketId}' was not found.");

        // Same bounded-context assembly as GenerateTicketSummaryCommandHandler (Story 25) —
        // duplicated deliberately rather than extracted into a shared helper.
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
            "You are an assistant that drafts a polite, helpful, customer-facing reply for a support " +
            "agent to review and send. Address the customer's issue directly based on the ticket context " +
            "below. Keep it concise and professional. Do not invent facts not present in the ticket. " +
            "Return only the reply text, with no explanation of your reasoning.";

        string suggestedReply;
        try
        {
            suggestedReply = await aiClient.GetCompletionAsync(systemPrompt, userPrompt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TicketReplySuggestionDto>.Failure(
                "AI reply suggestion is currently unavailable. Please try again later.");
        }

        return Result<TicketReplySuggestionDto>.Success(new TicketReplySuggestionDto(suggestedReply));
    }
}
