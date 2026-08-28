using AzmCrm.Application.Features.Communications.Commands.StartLiveChat;
using AzmCrm.Application.Tests.TestDoubles;
using Xunit;

namespace AzmCrm.Application.Tests.Features.Communications;

public class StartLiveChatCommandValidatorTests
{
    private static StartLiveChatCommandValidator CreateValidator() =>
        new(new StubLocalizationService());

    [Fact]
    public void Empty_Name_fails()
    {
        var result = CreateValidator().Validate(new StartLiveChatCommand("", "jane@example.com", "Body"));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_Email_fails()
    {
        var result = CreateValidator().Validate(new StartLiveChatCommand("Jane Doe", "not-an-email", "Body"));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Empty_Body_fails()
    {
        var result = CreateValidator().Validate(new StartLiveChatCommand("Jane Doe", "jane@example.com", ""));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = CreateValidator().Validate(
            new StartLiveChatCommand("Jane Doe", "jane@example.com", "Hi there"));
        Assert.True(result.IsValid);
    }
}
