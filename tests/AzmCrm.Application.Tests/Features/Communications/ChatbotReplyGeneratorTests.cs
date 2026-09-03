using AzmCrm.Application.Features.Communications;
using AzmCrm.Application.Tests.TestDoubles;
using AzmCrm.Domain.Features.KnowledgeBase;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class ChatbotReplyGeneratorTests
{
    [Fact]
    public async Task Generate_includes_matching_Published_article_content_in_prompt()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(new KnowledgeArticle
        {
            Title = "How do I reset my password?",
            Content = "Go to Settings > Security > Reset Password.",
            Type = KnowledgeArticleType.Faq,
            Status = KnowledgeArticleStatus.Published,
            PublishedOn = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var aiClient = new StubAiClient { Response = "Try resetting your password from Settings." };

        // The generator matches KB fields that CONTAIN the customer's message, so the message
        // here must itself appear verbatim inside the seeded article's content.
        var reply = await ChatbotReplyGenerator.GenerateAsync(dbContext, aiClient, "reset password", CancellationToken.None);

        Assert.Equal("Try resetting your password from Settings.", reply);
        Assert.Single(aiClient.Calls);
        Assert.Contains("Go to Settings > Security > Reset Password.", aiClient.Calls[0].SystemPrompt);
    }

    [Fact]
    public async Task Generate_excludes_Draft_articles_from_context()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        dbContext.KnowledgeArticles.Add(new KnowledgeArticle
        {
            Title = "password reset",
            Content = "Draft content that should never be shown to a customer.",
            Type = KnowledgeArticleType.Faq
        });
        await dbContext.SaveChangesAsync();

        var aiClient = new StubAiClient();

        await ChatbotReplyGenerator.GenerateAsync(dbContext, aiClient, "password reset", CancellationToken.None);

        Assert.DoesNotContain("Draft content", aiClient.Calls[0].SystemPrompt);
        Assert.Contains("No matching knowledge base articles were found.", aiClient.Calls[0].SystemPrompt);
    }

    [Fact]
    public async Task Generate_returns_fallback_message_when_AiClient_throws()
    {
        await using var dbContext = TestApplicationDbContext.Create();
        var aiClient = new StubAiClient { ThrowOnCall = true };

        var reply = await ChatbotReplyGenerator.GenerateAsync(dbContext, aiClient, "Hello", CancellationToken.None);

        Assert.Equal("Thanks for reaching out — one of our agents will follow up shortly.", reply);
    }
}
