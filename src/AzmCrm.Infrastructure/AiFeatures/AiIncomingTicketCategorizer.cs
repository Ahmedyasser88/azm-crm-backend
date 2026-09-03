using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.Tickets;
using Microsoft.Extensions.Logging;

namespace AzmCrm.Infrastructure.AiFeatures;

internal sealed class AiIncomingTicketCategorizer(IAiClient aiClient, ILogger<AiIncomingTicketCategorizer> logger)
    : IIncomingTicketCategorizer
{
    private static readonly string CategoryNames = string.Join(", ", Enum.GetNames<TicketCategory>());

    public async Task<TicketCategory> CategorizeAsync(string title, string? description, CancellationToken ct = default)
    {
        var systemPrompt =
            $"You classify support tickets into exactly one of these categories: {CategoryNames}. " +
            "Respond with only the category name, exactly as written above, with no punctuation or explanation.";

        var userPrompt = $"Title: {title}\nDescription: {description ?? "(none)"}";

        try
        {
            var response = await aiClient.GetCompletionAsync(systemPrompt, userPrompt, ct);

            if (Enum.TryParse<TicketCategory>(response.Trim(), ignoreCase: true, out var category))
                return category;

            logger.LogWarning("AI categorizer returned an unparseable category '{Response}'; falling back to General.", response);
            return TicketCategory.General;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "AI categorization failed; falling back to General.");
            return TicketCategory.General;
        }
    }
}
