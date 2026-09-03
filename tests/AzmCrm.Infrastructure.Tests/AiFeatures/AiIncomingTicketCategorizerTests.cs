using AzmCrm.Application.Shared.Interfaces;
using AzmCrm.Infrastructure.AiFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AzmCrm.Infrastructure.Tests.AiFeatures;

public class AiIncomingTicketCategorizerTests
{
    private sealed class FakeAiClient : IAiClient
    {
        public string Response { get; set; } = "General";
        public bool ThrowOnCall { get; set; }

        public Task<string> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        {
            if (ThrowOnCall)
                throw new InvalidOperationException("Simulated AI provider failure.");
            return Task.FromResult(Response);
        }
    }

    [Fact]
    public async Task Categorize_parses_valid_enum_name_case_insensitively()
    {
        var aiClient = new FakeAiClient { Response = "billing" };
        var categorizer = new AiIncomingTicketCategorizer(aiClient, NullLogger<AiIncomingTicketCategorizer>.Instance);

        var category = await categorizer.CategorizeAsync("Invoice question", "Why was I charged twice?");

        Assert.Equal(Domain.Features.Tickets.TicketCategory.Billing, category);
    }

    [Fact]
    public async Task Categorize_falls_back_to_General_on_unparseable_response()
    {
        var aiClient = new FakeAiClient { Response = "Not a real category!" };
        var categorizer = new AiIncomingTicketCategorizer(aiClient, NullLogger<AiIncomingTicketCategorizer>.Instance);

        var category = await categorizer.CategorizeAsync("Something odd", null);

        Assert.Equal(Domain.Features.Tickets.TicketCategory.General, category);
    }

    [Fact]
    public async Task Categorize_falls_back_to_General_when_AiClient_throws()
    {
        var aiClient = new FakeAiClient { ThrowOnCall = true };
        var categorizer = new AiIncomingTicketCategorizer(aiClient, NullLogger<AiIncomingTicketCategorizer>.Instance);

        var category = await categorizer.CategorizeAsync("Cannot log in", "401 error");

        Assert.Equal(Domain.Features.Tickets.TicketCategory.General, category);
    }
}
