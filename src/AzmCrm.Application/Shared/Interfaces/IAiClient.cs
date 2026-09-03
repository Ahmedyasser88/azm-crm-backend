namespace AzmCrm.Application.Shared.Interfaces;

/// <summary>
/// Provider-agnostic abstraction for getting a single text completion from an LLM, given a
/// system prompt (instructions/context) and a user prompt (the actual request). The Application
/// layer never touches a specific AI provider's HTTP API directly — swap the Infrastructure-layer
/// implementation without changing any handler that depends on this interface.
/// </summary>
public interface IAiClient
{
    Task<string> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
