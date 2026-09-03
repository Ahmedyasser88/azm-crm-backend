using AzmCrm.Application.Features.Communications.Commands.StartAiChat;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class StartAiChatCommandValidatorTests
{
    private static StartAiChatCommandValidator CreateValidator() =>
        new(new StubLocalizationService());

    [Fact]
    public void Empty_Name_fails()
    {
        var result = CreateValidator().Validate(new StartAiChatCommand("", "jane@example.com", "Body"));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_Email_fails()
    {
        var result = CreateValidator().Validate(new StartAiChatCommand("Jane Doe", "not-an-email", "Body"));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_Body_fails()
    {
        var result = CreateValidator().Validate(new StartAiChatCommand("Jane Doe", "jane@example.com", ""));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = CreateValidator().Validate(
            new StartAiChatCommand("Jane Doe", "jane@example.com", "Hi there"));
        Assert.True(result.IsValid);
    }
}
