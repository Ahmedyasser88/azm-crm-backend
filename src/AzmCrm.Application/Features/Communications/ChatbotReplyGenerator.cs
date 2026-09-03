using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Domain.Features.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace AzmCrm.Application.Features.Communications;

/// <summary>
/// Generates a knowledge-base-grounded AI reply to a customer's chatbot message. Shared by
/// StartAiChatCommandHandler and SendChatbotMessageCommandHandler. Never throws — a caller-visible
/// failure to generate a reply must never break the chatbot flow (see FallbackReply).
/// </summary>
internal static class ChatbotReplyGenerator
{
    private const string FallbackReply = "Thanks for reaching out — one of our agents will follow up shortly.";

    public static async Task<string> GenerateAsync(
        IApplicationDbContext dbContext, IAiClient aiClient, string customerMessage, CancellationToken ct)
    {
        var term = customerMessage.Trim().ToLower();

        var articles = await dbContext.KnowledgeArticles
            .Where(a => a.Status == KnowledgeArticleStatus.Published)
            .Where(a => a.Title.ToLower().Contains(term) || a.Content.ToLower().Contains(term))
            .OrderByDescending(a => a.PublishedOn)
            .Take(3)
            .Select(a => new { a.Title, a.Content })
            .ToListAsync(ct);

        var context = articles.Count > 0
            ? string.Join("\n\n", articles.Select(a => $"Article: {a.Title}\n{a.Content}"))
            : "No matching knowledge base articles were found.";

        var systemPrompt =
            "You are a customer self-service chatbot for a support team. Answer the customer's message " +
            "using only the knowledge base context below. If the context does not contain a relevant " +
            "answer, politely say a human agent will follow up. Keep answers short and friendly.\n\n" +
            context;

        try
        {
            return await aiClient.GetCompletionAsync(systemPrompt, customerMessage, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FallbackReply;
        }
    }
}
