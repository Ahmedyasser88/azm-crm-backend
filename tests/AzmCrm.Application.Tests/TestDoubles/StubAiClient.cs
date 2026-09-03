using AzmCrm.Application.Shared.Interfaces;

namespace AzmCrm.Application.Tests.TestDoubles;

public sealed class StubAiClient : IAiClient
{
    public List<(string SystemPrompt, string UserPrompt)> Calls { get; } = [];
    public string Response { get; set; } = "Stub AI summary.";
    public bool ThrowOnCall { get; set; }

    public Task<string> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (ThrowOnCall)
            throw new InvalidOperationException("Simulated AI provider failure.");
        Calls.Add((systemPrompt, userPrompt));
        return Task.FromResult(Response);
    }
}
